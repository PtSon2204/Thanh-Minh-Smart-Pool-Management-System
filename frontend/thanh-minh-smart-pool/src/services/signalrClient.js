import * as signalR from '@microsoft/signalr';
import { getAccessToken, useAuthStore } from '../features/auth/store/authStore';
import { resolveSignalRHubUrl } from './signalrUrl';

let connection = null;
let authenticatedToken = null;
let lifecycle = Promise.resolve();
const listeners = new Set();
const waterQualityListeners = new Set();
const waterQualityDangerListeners = new Set();
const stateListeners = new Set();
let retryTimer = null;
let retryAttempt = 0;
const retryDelays = [2000, 5000, 10000, 30000];

function dispatchIfAuthenticated(targetListeners, payload, errorLabel) {
  if (!authenticatedToken || authenticatedToken !== getAccessToken()) return;
  targetListeners.forEach((listener) => {
    try {
      listener(payload);
    } catch (err) {
      console.error(errorLabel, err);
    }
  });
}

function clearRetry(reset = false) {
  if (retryTimer !== null) clearTimeout(retryTimer);
  retryTimer = null;
  if (reset) retryAttempt = 0;
}

// SignalR automatic reconnect only covers connections that have already started.
function scheduleRetry() {
  if (retryTimer !== null || !getAccessToken()) return;
  const delay = retryDelays[Math.min(retryAttempt++, retryDelays.length - 1)];
  retryTimer = setTimeout(() => {
    retryTimer = null;
    void startSignalRConnection();
  }, delay);
}

// Preserve listeners on the same HubConnection, but never preserve its principal
// across logout/account/token changes. Serializing stop/start also avoids races
// between the camera, popup and notification hooks starting at the same time.
useAuthStore.subscribe((state, previous) => {
  if (connection && signalRIdentity(state.session) !== signalRIdentity(previous.session))
    void startSignalRConnection();
});

/**
 * Lấy hoặc khởi tạo instance HubConnection kết nối tới /hubs/notifications
 */
export function getSignalRConnection() {
  if (connection) return connection;

  const rawBase = import.meta.env.VITE_SIGNALR_HUB_URL || import.meta.env.VITE_API_BASE_URL || window.location.origin;
  const hubUrl = resolveSignalRHubUrl(rawBase, window.location.origin);

  connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => getAccessToken() || '',
      skipNegotiation: false,
      transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  connection.on('ReceiveNotification', (payload) => {
    dispatchIfAuthenticated(listeners, payload, 'Error in notification listener:');
  });

  connection.on('WaterQualityUpdated', (payload) => {
    dispatchIfAuthenticated(waterQualityListeners, payload, 'Error in water quality listener:');
  });

  connection.on('water_quality_danger', (payload) => {
    dispatchIfAuthenticated(waterQualityListeners, payload, 'Error in water quality danger listener:');
    dispatchIfAuthenticated(waterQualityDangerListeners, payload, 'Error in water quality danger listener:');
  });

  connection.onreconnecting((error) => {
    stateListeners.forEach((fn) => fn('reconnecting', error));
  });

  connection.onreconnected((connectionId) => {
    clearRetry(true);
    stateListeners.forEach((fn) => fn('connected', connectionId));
  });

  connection.onclose((error) => {
    stateListeners.forEach((fn) => fn('disconnected', error));
    scheduleRetry();
  });

  return connection;
}

/**
 * Đăng ký lắng nghe sự kiện ReceiveNotification
 * @param {Function} callback (payload) => void
 * @returns {Function} unsubscribe function
 */
export function onReceiveNotification(callback) {
  listeners.add(callback);
  return () => {
    listeners.delete(callback);
  };
}

/**
 * Đăng ký lắng nghe cập nhật chất lượng nước / lịch trực bể real-time
 * @param {Function} callback (payload) => void
 * @returns {Function} unsubscribe function
 */
export function onWaterQualityUpdated(callback) {
  waterQualityListeners.add(callback);
  return () => {
    waterQualityListeners.delete(callback);
  };
}

/**
 * Đăng ký lắng nghe cảnh báo chất lượng nước mức NGUY HIỂM real-time
 * @param {Function} callback (payload) => void
 * @returns {Function} unsubscribe function
 */
export function onWaterQualityDanger(callback) {
  waterQualityDangerListeners.add(callback);
  return () => {
    waterQualityDangerListeners.delete(callback);
  };
}

/**
 * Đăng ký lắng nghe thay đổi trạng thái kết nối
 */
export function onSignalRStateChange(callback) {
  stateListeners.add(callback);
  return () => {
    stateListeners.delete(callback);
  };
}

/**
 * Dấu hiệu nhận dạng của phiên dùng để biết kết nối SignalR có cần (lại) khởi
 * động không: chỉ định khi có token hợp lệ (một người dùng một kết nối).
 * undefined khi chưa đăng nhập → các hook chờ đăng nhập rồi mới nối.
 */
export function signalRIdentity(session) {
  return session?.accessToken ? `${session.id}|${session.accessToken}` : undefined
}

/**
 * Khởi động kết nối SignalR (nếu chưa kết nối)
 */
export function startSignalRConnection() {
  const conn = getSignalRConnection();
  const synchronize = async () => {
    clearRetry();
    try {
      while (true) {
        const token = getAccessToken();
        if (conn.state !== signalR.HubConnectionState.Disconnected && token !== authenticatedToken)
          await conn.stop();
        if (!token) { authenticatedToken = null; clearRetry(true); return false; }
        if (conn.state === signalR.HubConnectionState.Disconnected) {
          authenticatedToken = token;
          await conn.start();
          // A logout/refresh may have happened during the handshake.
          if (token !== getAccessToken()) continue;
          clearRetry(true);
          stateListeners.forEach((fn) => fn('connected', conn.connectionId));
        }
        return conn.state === signalR.HubConnectionState.Connected;
      }
    } catch (err) {
      stateListeners.forEach((fn) => fn('error', err));
      scheduleRetry();
      return false;
    }
  };
  const pending = lifecycle.then(synchronize, synchronize);
  lifecycle = pending.catch(() => false);
  return pending;
}

/**
 * Gửi thông báo test trực tiếp qua Hub (nếu có method)
 */
export async function sendNotificationViaHub(title, content, type = 'SYSTEM') {
  if (!await startSignalRConnection()) return false;
  const conn = getSignalRConnection();
  if (conn.state === signalR.HubConnectionState.Connected) {
    try {
      await conn.invoke('SendNotification', title, content, type);
      return true;
    } catch {
      return false;
    }
  }
  return false;
}
