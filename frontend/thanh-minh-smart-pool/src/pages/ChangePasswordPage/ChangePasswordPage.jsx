import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link, useLocation } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  Eye,
  EyeOff,
  KeyRound,
  LockKeyhole,
  ShieldCheck,
} from "lucide-react";

import { changePasswordFormSchema } from "../../features/auth/types/authSchemas";
import authService from "../../features/auth/services/authService";
import "./ChangePasswordPage.css";

const fields = [
  {
    name: "currentPassword",
    label: "Mật khẩu hiện tại",
    autoComplete: "current-password",
    placeholder: "Nhập mật khẩu hiện tại",
  },
  {
    name: "newPassword",
    label: "Mật khẩu mới",
    autoComplete: "new-password",
    placeholder: "Nhập mật khẩu mới",
  },
  {
    name: "confirmPassword",
    label: "Xác nhận mật khẩu mới",
    autoComplete: "new-password",
    placeholder: "Nhập lại mật khẩu mới",
  },
];

export default function ChangePasswordPage() {
  const location = useLocation();

  const backTo = location.pathname.startsWith("/admin") ? "/admin" : "/ho-so";

  const [visible, setVisible] = useState({});
  const [loading, setLoading] = useState(false);
  const [feedback, setFeedback] = useState(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(changePasswordFormSchema),
    defaultValues: {
      currentPassword: "",
      newPassword: "",
      confirmPassword: "",
    },
  });

  const onSubmit = async (values) => {
    setLoading(true);
    setFeedback(null);

    try {
      await authService.changePassword(values);

      setFeedback({
        type: "success",
        message: "Đổi mật khẩu thành công.",
      });

      reset();
      setVisible({});
    } catch (error) {
      const data = error.response?.data;

      const validationMessage = Object.values(data?.errors || {}).flat()[0];

      const message =
        data?.detail ||
        data?.message ||
        validationMessage ||
        data?.title ||
        (error.response?.status === 401
          ? "Phiên đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại."
          : error.response?.status === 403
            ? "Bạn không có quyền thực hiện thao tác này."
            : error.message || "Đổi mật khẩu thất bại. Vui lòng thử lại.");

      setFeedback({
        type: "error",
        message,
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="change-password-page">
      <div className="change-password-banner" />

      <div className="change-password-container">
        <Link className="change-password-back" to={backTo}>
          <ArrowLeft size={16} aria-hidden="true" />
          Quay lại {backTo === "/admin" ? "bảng điều khiển" : "hồ sơ"}
        </Link>

        <div className="change-password-card">
          <div className="change-password-heading">
            <div className="change-password-icon">
              <KeyRound size={27} aria-hidden="true" />
            </div>

            <div>
              <p className="change-password-eyebrow">BẢO MẬT TÀI KHOẢN</p>

              <h1>Đổi mật khẩu</h1>

              <p>
                Cập nhật mật khẩu thường xuyên để bảo vệ tài khoản Thành Minh
                của bạn.
              </p>
            </div>
          </div>

          <form onSubmit={handleSubmit(onSubmit)} noValidate>
            {fields.map(({ name, label, autoComplete, placeholder }) => (
              <div className="change-password-field" key={name}>
                <label htmlFor={name}>{label}</label>

                <div
                  className={`change-password-input${
                    errors[name] ? " change-password-input-invalid" : ""
                  }`}
                >
                  <LockKeyhole size={18} aria-hidden="true" />

                  <input
                    id={name}
                    type={visible[name] ? "text" : "password"}
                    placeholder={placeholder}
                    autoComplete={autoComplete}
                    aria-invalid={Boolean(errors[name])}
                    aria-describedby={
                      errors[name] ? `${name}-error` : undefined
                    }
                    {...register(name, {
                      onChange: () => setFeedback(null),
                    })}
                  />

                  <button
                    type="button"
                    className="change-password-toggle"
                    onClick={() =>
                      setVisible((previous) => ({
                        ...previous,
                        [name]: !previous[name],
                      }))
                    }
                    aria-label={
                      visible[name]
                        ? `Ẩn ${label.toLowerCase()}`
                        : `Hiện ${label.toLowerCase()}`
                    }
                  >
                    {visible[name] ? (
                      <EyeOff size={18} aria-hidden="true" />
                    ) : (
                      <Eye size={18} aria-hidden="true" />
                    )}
                  </button>
                </div>

                {errors[name] && (
                  <p
                    className="change-password-error"
                    id={`${name}-error`}
                    role="alert"
                  >
                    {errors[name].message}
                  </p>
                )}
              </div>
            ))}

            <div className="change-password-tip">
              <ShieldCheck size={19} aria-hidden="true" />

              <span>
                Mật khẩu mới cần từ 8 đến 128 ký tự, gồm ít nhất một chữ cái và
                một chữ số.
              </span>
            </div>

            {feedback && (
              <p
                className={`change-password-notice${
                  feedback.type === "error" ? " change-password-error" : ""
                }`}
                role={feedback.type === "error" ? "alert" : "status"}
              >
                {feedback.message}
              </p>
            )}

            <div className="change-password-actions">
              <Link
                to={backTo}
                className="change-password-cancel"
                aria-disabled={loading}
                onClick={(event) => {
                  if (loading) event.preventDefault();
                }}
              >
                Hủy
              </Link>

              <button
                type="submit"
                className="change-password-submit"
                disabled={loading}
              >
                {loading ? "Đang cập nhật..." : "Đổi mật khẩu"}

                {!loading && <ArrowRight size={18} aria-hidden="true" />}
              </button>
            </div>
          </form>
        </div>
      </div>
    </main>
  );
}
