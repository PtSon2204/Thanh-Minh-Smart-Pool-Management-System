import * as signalR from '@microsoft/signalr';
import { getAccessToken } from '../features/auth/store/authStore';

let connection = null;
const listeners = new Set();
const stateListeners = new Set();

/**
 * Lấy hoặc khởi tạo instance HubConnection kết nối tới /hubs/notifications
 */
export function getSignalRConnection() {
  if (connection) return connection;

  const rawBase = import.meta.env.VITE_SIGNALR_HUB_URL || import.meta.env.VITE_API_BASE_URL || window.location.origin;
  const baseUrl = rawBase.replace(/\/$/, '');
  const hubUrl = `${baseUrl}/hubs/notifications`;

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
    listeners.forEach((listener) => {
      try {
        listener(payload);
      } catch (err) {
        console.error('Error in notification listener:', err);
      }
    });
  });

  connection.onreconnecting((error) => {
    stateListeners.forEach((fn) => fn('reconnecting', error));
  });

  connection.onreconnected((connectionId) => {
    stateListeners.forEach((fn) => fn('connected', connectionId));
  });

  connection.onclose((error) => {
    stateListeners.forEach((fn) => fn('disconnected', error));
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
 * Đăng ký lắng nghe thay đổi trạng thái kết nối
 */
export function onSignalRStateChange(callback) {
  stateListeners.add(callback);
  return () => {
    stateListeners.delete(callback);
  };
}

/**
 * Khởi động kết nối SignalR (nếu chưa kết nối)
 */
export async function startSignalRConnection() {
  const conn = getSignalRConnection();
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    try {
      await conn.start();
      stateListeners.forEach((fn) => fn('connected', conn.connectionId));
      return true;
    } catch (err) {
      stateListeners.forEach((fn) => fn('error', err));
      return false;
    }
  }
  return conn.state === signalR.HubConnectionState.Connected;
}

/**
 * Gửi thông báo test trực tiếp qua Hub (nếu có method)
 */
export async function sendNotificationViaHub(title, content, type = 'SYSTEM') {
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
