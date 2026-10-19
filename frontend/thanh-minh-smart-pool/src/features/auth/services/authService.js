import apiClient from "../../../services/apiClient";
import { useAuthStore } from "../store/authStore";
import {
  loginResponseSchema,
  registerPendingResponseSchema,
  resendRegistrationResponseSchema,
  verifiedRegistrationResponseSchema,
  requestPasswordResetResponseSchema,
  resendPasswordResetResponseSchema,
  verifyPasswordResetResponseSchema,
} from "../types/authSchemas";

const authService = {
  async login(payload) {
    const response = await apiClient.post("/api/auth/login", payload);
    return loginResponseSchema.parse(response.data);
  },

  async logout() {
    const token = useAuthStore.getState().session?.refreshToken;
    useAuthStore.getState().setSession(null);
    if (!token) return;
    const response = await apiClient.post("/api/auth/logout", { refreshToken: token });
    if (response.status !== 204) throw new Error("Phản hồi đăng xuất không hợp lệ.");
  },

  async register(payload) {
    const response = await apiClient.post("/api/auth/register", payload);
    if (response.status !== 202)
      throw new Error("Phản hồi đăng ký không hợp lệ.");
    return registerPendingResponseSchema.parse(response.data);
  },

  async verifyRegistration(payload) {
    const response = await apiClient.post("/api/auth/register/verify", payload);
    if (response.status !== 201)
      throw new Error("Phản hồi xác minh không hợp lệ.");
    return verifiedRegistrationResponseSchema.parse(response.data);
  },

  async resendRegistration(payload) {
    const response = await apiClient.post("/api/auth/register/resend", payload);
    if (response.status !== 202)
      throw new Error("Phản hồi gửi lại không hợp lệ.");
    return resendRegistrationResponseSchema.parse(response.data);
  },

  async requestPasswordReset(payload) {
    const response = await apiClient.post(
      "/api/auth/password-reset/request",
      payload,
    );
    if (response.status !== 202)
      throw new Error("Phản hồi yêu cầu khôi phục không hợp lệ.");
    return requestPasswordResetResponseSchema.parse(response.data);
  },

  async resendPasswordReset(payload) {
    const response = await apiClient.post(
      "/api/auth/password-reset/resend",
      payload,
    );
    if (response.status !== 202)
      throw new Error("Phản hồi gửi lại không hợp lệ.");
    return resendPasswordResetResponseSchema.parse(response.data);
  },

  async verifyPasswordReset(payload) {
    const response = await apiClient.post(
      "/api/auth/password-reset/verify",
      payload,
    );
    if (response.status !== 200)
      throw new Error("Phản hồi xác minh không hợp lệ.");
    return verifyPasswordResetResponseSchema.parse(response.data);
  },

  async completePasswordReset(payload) {
    const response = await apiClient.post(
      "/api/auth/password-reset/complete",
      payload,
    );
    if (response.status !== 204)
      throw new Error("Phản hồi đặt lại mật khẩu không hợp lệ.");
    useAuthStore.getState().setSession(null);
  },

  async changePassword(payload) {
    const response = await apiClient.put("/api/auth/change-password", {
      currentPassword: payload.currentPassword,
      newPassword: payload.newPassword,
      confirmNewPassword: payload.confirmPassword,
    });

    if (response.status !== 204) {
      throw new Error("Phản hồi đổi mật khẩu không hợp lệ.");
    }
    useAuthStore.getState().setSession(null);
  },
};

export default authService;
