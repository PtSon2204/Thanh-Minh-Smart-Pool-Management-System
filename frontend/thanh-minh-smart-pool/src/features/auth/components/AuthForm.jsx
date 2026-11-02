import { useEffect, useRef, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  useGoogleLogin,
  useLogin,
  useRegister,
} from "../hooks/useAuthMutations";
import { loadGoogleIdentity } from "../services/googleIdentity";
import GoogleProfileForm from "./GoogleProfileForm";
import { savePendingRegistration } from "../state/registrationSession";
import { beginOtpRequest } from "../state/otpRequest";
import { getLandingPath } from "../utils/sessionRoles";
import {
  getAuthError,
  loginFormSchema,
  registerFormSchema,
} from "../types/authSchemas";

function AuthField({ id, label, error, children }) {
  const errorId = `${id}-error`;
  return (
    <div className="auth-field-group">
      <label className="auth-field-label" htmlFor={id}>
        {label}
      </label>
      <div className={`auth-field${error ? " auth-field-invalid" : ""}`}>
        {children(errorId)}
      </div>
      {error && (
        <p className="auth-error-text" id={errorId} role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

export default function AuthForm({ isRegister }) {
  const location = useLocation();
  const navigate = useNavigate();
  const [visiblePassword, setVisiblePassword] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [googleCredential, setGoogleCredential] = useState(null);
  const submittingRef = useRef(false);
  const googleButtonRef = useRef(null);
  const googleCallbackRef = useRef(null);
  const login = useLogin();
  const googleLogin = useGoogleLogin();
  const register = useRegister();
  const pending =
    login.isPending || googleLogin.isPending || register.isPending;
  const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
  const loginForm = useForm({
    resolver: zodResolver(loginFormSchema),
    defaultValues: {
      identifier: location.state?.registeredUsername ?? "",
      password: "",
    },
  });
  const registerForm = useForm({
    resolver: zodResolver(registerFormSchema),
    defaultValues: {
      phone: "",
      email: "",
      password: "",
    },
  });
  const form = isRegister ? registerForm : loginForm;
  const feedback = isRegister
    ? errorMessage
    : errorMessage ||
      location.state?.registrationSuccess ||
      location.state?.resetSuccess ||
      location.state?.passwordChanged;
  const errorFor = (name) => form.formState.errors[name]?.message;

  useEffect(() => {
    googleCallbackRef.current = async ({ credential }) => {
      if (submittingRef.current || !credential) return;
      submittingRef.current = true;
      setErrorMessage("");
      try {
        const session = await googleLogin.mutateAsync(credential);
        loginForm.setValue("password", "");
        registerForm.setValue("password", "");
        navigate(getLandingPath(session));
      } catch (error) {
        if (error?.response?.status === 409 &&
            error.response.data?.code === 'GOOGLE_ACCOUNT_NOT_REGISTERED') {
          setGoogleCredential(credential);
          return;
        }
        setErrorMessage(getAuthError(error).message);
      } finally {
        submittingRef.current = false;
      }
    };
  });

  useEffect(() => {
    if (!googleClientId || googleCredential || !googleButtonRef.current) return;
    let active = true;
    loadGoogleIdentity()
      .then((google) => {
        if (!active || !googleButtonRef.current) return;
        google.accounts.id.initialize({
          client_id: googleClientId,
          callback: (response) => googleCallbackRef.current?.(response),
        });
        googleButtonRef.current.replaceChildren();
        google.accounts.id.renderButton(googleButtonRef.current, {
          type: "standard",
          theme: "outline",
          size: "large",
          text: isRegister ? "signup_with" : "signin_with",
          width: 320,
        });
      })
      .catch((error) => {
        if (active) setErrorMessage(error.message);
      });
    return () => {
      active = false;
    };
  }, [googleClientId, isRegister, googleCredential]);

  function clearPasswords() {
    loginForm.setValue("password", "");
    registerForm.setValue("password", "");
  }

  function changeTab(nextPath) {
    if (pending) return;
    clearPasswords();
    navigate(nextPath);
  }

  function applyServerErrors(fieldErrors) {
    Object.entries(fieldErrors).forEach(([field, message]) => {
      if (field in form.getValues())
        form.setError(field, { type: "server", message });
    });
  }

  async function submitLogin(values) {
    if (submittingRef.current) return;
    submittingRef.current = true;
    setErrorMessage("");
    try {
      const session = await login.mutateAsync(values);
      clearPasswords();
      navigate(getLandingPath(session));
    } catch (error) {
      const authError = getAuthError(error);
      applyServerErrors(authError.fieldErrors);
      setErrorMessage(authError.message);
    } finally {
      submittingRef.current = false;
    }
  }

  function submitRegister(values) {
    if (submittingRef.current) return;
    submittingRef.current = true;
    setErrorMessage("");
    const email = values.email.trim();
    const request = register
      .mutateAsync({
        email: values.email,
        phone: values.phone,
        password: values.password,
      })
      .then((pendingRegistration) => {
        const session = { ...pendingRegistration, email };
        savePendingRegistration(session);
        return session;
      });
    const requestId = beginOtpRequest("register", email, request);
    clearPasswords();
    navigate("/xac-nhan-otp/dang-ky", {
      replace: true,
      state: { email, requestId },
    });
  }

  function onSubmit(event) {
    event.preventDefault();
    if (submittingRef.current) return;
    void form.handleSubmit(isRegister ? submitRegister : submitLogin)(event);
  }

  const mailIcon = (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      width="18"
      height="18"
    >
      <rect x="2" y="4" width="20" height="16" rx="2" />
      <path d="m2 7 10 7 10-7" />
    </svg>
  );

  if (googleCredential) {
    const values = registerForm.getValues();
    return <GoogleProfileForm credential={googleCredential} initialValues={{
      username: "",
      phone: values.phone,
      fullName: "",
    }} onSuccess={(session) => {
      clearPasswords();
      navigate(getLandingPath(session));
    }} onCancel={() => setGoogleCredential(null)} />;
  }

  return (
    <div className="auth-card" id="auth-card">
      <div className="auth-tabs">
        <button
          className={`auth-tab${!isRegister ? " active" : ""}`}
          onClick={() => changeTab("/dang-nhap")}
          type="button"
          disabled={pending}
        >
          Đăng nhập
        </button>
        <button
          className={`auth-tab${isRegister ? " active" : ""}`}
          onClick={() => changeTab("/dang-ky")}
          type="button"
          disabled={pending}
        >
          Đăng ký
        </button>
      </div>
      <h1 className="auth-card-title">
        {isRegister ? "" : "Chào mừng trở lại!"}
      </h1>
      <form onSubmit={onSubmit} noValidate>
        {isRegister && (
          <>
            <AuthField
              id="inp-phone"
              label="Số điện thoại"
              error={errorFor("phone")}
            >
              {(errorId) => (
                <>
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    width="18"
                    height="18"
                  >
                    <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.8 19.8 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.12 4.18 2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.12.9.33 1.78.62 2.63a2 2 0 0 1-.45 2.11L7.99 9.75a16 16 0 0 0 6 6l1.29-1.29a2 2 0 0 1 2.11-.45c.85.29 1.73.5 2.63.62A2 2 0 0 1 22 16.92z" />
                  </svg>
                  <input
                    id="inp-phone"
                    type="tel"
                    placeholder="0912345678"
                    autoComplete="tel"
                    disabled={pending}
                    aria-invalid={Boolean(errorFor("phone"))}
                    aria-describedby={errorFor("phone") ? errorId : undefined}
                    {...registerForm.register("phone")}
                  />
                </>
              )}
            </AuthField>
          </>
        )}
        <AuthField
          id="inp-email"
          label={
            isRegister ? "Email" : "Tên đăng nhập, email hoặc số điện thoại"
          }
          error={errorFor(isRegister ? "email" : "identifier")}
        >
          {(errorId) => (
            <>
              {mailIcon}
              <input
                type={isRegister ? "email" : "text"}
                id="inp-email"
                placeholder={
                  isRegister
                    ? "Eg. johndoe@gmail.com"
                    : "Tên đăng nhập, email hoặc số điện thoại"
                }
                autoComplete={isRegister ? "email" : "username"}
                aria-label={
                  isRegister
                    ? "Email"
                    : "Tên đăng nhập, email hoặc số điện thoại"
                }
                disabled={pending}
                aria-invalid={Boolean(
                  errorFor(isRegister ? "email" : "identifier"),
                )}
                aria-describedby={
                  errorFor(isRegister ? "email" : "identifier")
                    ? errorId
                    : undefined
                }
                {...(isRegister
                  ? registerForm.register("email")
                  : loginForm.register("identifier"))}
              />
            </>
          )}
        </AuthField>
        <AuthField id="inp-pw" label="Mật khẩu" error={errorFor("password")}>
          {(errorId) => (
            <>
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                width="18"
                height="18"
              >
                <rect x="3" y="11" width="18" height="11" rx="2" />
                <path d="M7 11V7a5 5 0 0 1 10 0v4" />
              </svg>
              <input
                type={visiblePassword ? "text" : "password"}
                id="inp-pw"
                placeholder="Mật khẩu"
                autoComplete={isRegister ? "new-password" : "current-password"}
                disabled={pending}
                aria-invalid={Boolean(errorFor("password"))}
                aria-describedby={errorFor("password") ? errorId : undefined}
                {...form.register("password")}
              />
              <button
                className="auth-eye-btn"
                type="button"
                onClick={() => setVisiblePassword((current) => !current)}
                aria-label="Hiện/ẩn mật khẩu"
                disabled={pending}
              >
                {visiblePassword ? (
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    width="18"
                    height="18"
                  >
                    <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                    <line
                      x1="1"
                      y1="1"
                      x2="23"
                      y2="23"
                      stroke="currentColor"
                      strokeWidth="2"
                    />
                  </svg>
                ) : (
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    width="18"
                    height="18"
                  >
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                    <circle cx="12" cy="12" r="3" />
                  </svg>
                )}
              </button>
            </>
          )}
        </AuthField>
        {!isRegister && (
          <div className="auth-forgot-row">
            <Link to="/quen-mat-khau" className="auth-forgot-link">
              Quên mật khẩu?
            </Link>
          </div>
        )}
        {feedback && (
          <p
            className={`auth-feedback${errorMessage ? " auth-feedback-error" : ""}`}
            role="alert"
          >
            {feedback}
          </p>
        )}
        <button
          className="auth-btn-main"
          id="loginBtn"
          type="submit"
          disabled={pending}
        >
          <span>
            {pending ? "Đang xử lý..." : isRegister ? "Đăng ký" : "Đăng nhập"}
          </span>
          <svg viewBox="0 0 22 22" fill="none" width="18" height="18">
            <path
              d="M3 11h15.4M11 3.3l7.7 7.7-7.7 7.7"
              stroke="#fff"
              strokeWidth="2.6"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>
      </form>
      <div className="auth-divider">
        <i />
        <b>HOẶC</b>
        <i />
      </div>
      {googleClientId ? (
        <div
          id="gBtn"
          ref={googleButtonRef}
          style={{
            display: "flex",
            justifyContent: "center",
            pointerEvents: pending ? "none" : "auto",
            opacity: pending ? 0.6 : 1,
          }}
        />
      ) : (
        <button
          type="button"
          className="auth-btn-google-disabled"
          disabled
          title="Đăng nhập Google tạm thời chưa khả dụng"
        >
          <svg width="18" height="18" viewBox="0 0 24 24">
            <path fill="#4285F4" d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v4.51h6.6c-.29 1.52-1.14 2.82-2.4 3.68v3.05h3.88c2.27-2.09 3.665-5.17 3.665-9.17z"/>
            <path fill="#34A853" d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.88-3.05c-1.08.72-2.45 1.16-4.05 1.16-3.12 0-5.77-2.1-6.72-4.93H1.24v3.15C3.26 21.36 7.33 24 12 24z"/>
            <path fill="#FBBC05" d="M5.28 14.27c-.25-.72-.38-1.49-.38-2.27s.13-1.55.38-2.27V6.58H1.24C.45 8.15 0 9.99 0 12s.45 3.85 1.24 5.42l4.04-3.15z"/>
            <path fill="#EA4335" d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.33 0 3.26 2.64 1.24 6.58l4.04 3.15c.95-2.83 3.6-4.93 6.72-4.93z"/>
          </svg>
          <span>Đăng nhập với Google</span>
        </button>
      )}
      <p className="auth-footer-text">
        {isRegister ? "Đã có tài khoản? " : "Chưa có tài khoản? "}
        <button
          type="button"
          className="auth-footer-link"
          onClick={() => changeTab(isRegister ? "/dang-nhap" : "/dang-ky")}
          disabled={pending}
        >
          {isRegister ? "Đăng nhập" : "Đăng ký miễn phí"}
        </button>
      </p>
    </div>
  );
}
