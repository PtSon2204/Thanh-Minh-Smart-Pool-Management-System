import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Button,
  Descriptions,
  Form,
  Input,
  Modal,
  Spin,
  Typography,
} from "antd";
import userService from "../../users/services/userService";
import {
  getStaffErrorMessage,
  setStaffFormErrors,
  useResetStaffPassword,
} from "../hooks/useStaffMutations";
import { STAFFS_QUERY_KEY } from "../hooks/useStaffs";
import "../styles/staffContracts.css";

const RESET_FAILED_MESSAGE =
  "Mật khẩu cũ không còn hiệu lực. Chưa gửi được email. Admin phải đặt lại mật khẩu để gửi lại.";

// Prefill must run inside the connected Form: the modal mounts its content
// lazily, so a parent-level setFieldsValue targets an unconnected useForm
// instance and is dropped (AntD warning).
function EmailPrefill({ form, open, next, userId }) {
  useEffect(() => {
    if (!open || !next) return;
    const touched = form.isFieldTouched("personalEmail");
    const current = form.getFieldValue("personalEmail");
    if (touched && current && current !== next) return; // keep an Admin edit
    form.setFieldsValue({ personalEmail: next });
  }, [form, open, next, userId]);
  return null;
}

export default function StaffPasswordResetModal({ open, staff, onClose }) {
  const [form] = Form.useForm();
  const submitting = useRef(false);
  const [result, setResult] = useState(null);
  const queryClient = useQueryClient();
  const userId = staff?.userId;
  const storedEmail = staff?.email || "";
  const detailQuery = useQuery({
    queryKey: ["users", "detail", userId, "staff-reset"],
    queryFn: () => userService.getById(userId),
    enabled: open && Boolean(userId) && !storedEmail,
    retry: false,
    staleTime: 30_000,
  });
  const resetMutation = useResetStaffPassword();
  const loadingPrefill = open && !storedEmail && detailQuery.isLoading;
  const loadedEmail = detailQuery.data?.email || "";
  const close = () => {
    if (resetMutation.isPending || submitting.current) return;
    form.resetFields();
    setResult(null);
    resetMutation.reset();
    onClose();
  };

  const handleFinish = (values) => {
    if (submitting.current || resetMutation.isPending || result) return;
    submitting.current = true;
    const personalEmail = values.personalEmail.trim().toLowerCase();
    resetMutation.mutate(
      { userId, personalEmail },
      {
        onSuccess: (data) => {
          setResult({ ...data, personalEmail });
          form.resetFields();
          queryClient.invalidateQueries({ queryKey: STAFFS_QUERY_KEY });
          queryClient.invalidateQueries({ queryKey: ["staff-edit", userId] });
          queryClient.invalidateQueries({ queryKey: ["users", "detail", userId] });
        },
        onError: (failure) => {
          setStaffFormErrors(form, failure);
        },
        onSettled: () => {
          submitting.current = false;
        },
      },
    );
  };

  const description = result
    ? null
    : "Hệ thống gửi mật khẩu tạm thời tới email cá nhân. Nhân viên phải đổi mật khẩu sau khi đăng nhập lần đầu. Mật khẩu tạm thời không hiển thị ở đây.";

  return (
    <Modal
      title="Đặt lại mật khẩu nhân viên"
      open={open}
      onCancel={close}
      maskClosable={!resetMutation.isPending}
      destroyOnHidden
      width={560}
      footer={
        result
          ? [
              <Button key="close" type="primary" onClick={close}>
                Đóng
              </Button>,
            ]
          : [
              <Button key="cancel" onClick={close} disabled={resetMutation.isPending}>
                Huỷ
              </Button>,
              <Button
                key="submit"
                type="primary"
                onClick={() => form.submit()}
                loading={resetMutation.isPending}
                disabled={loadingPrefill}
              >
                Đặt lại mật khẩu
              </Button>,
            ]
      }
    >
      <div className="staff-contract-stack">
        {description && (
          <Alert type="info" showIcon title="Gửi mật khẩu tạm thời" description={description} />
        )}
        {staff?.fullName && <Typography.Text strong>{staff.fullName}</Typography.Text>}
        {detailQuery.isError && !storedEmail && (
          <Alert
            type="warning"
            showIcon
            title="Không tải được email đã lưu"
            description="Nhập email cá nhân của nhân viên trước khi đặt lại mật khẩu."
          />
        )}
        {resetMutation.isError && !result && (
          <Alert
            type="error"
            showIcon
            title="Không thể đặt lại mật khẩu"
            description={getStaffErrorMessage(
              resetMutation.error,
              "Máy chủ không thể đặt lại mật khẩu. Thử lại sau.",
            )}
          />
        )}
        {result && (
          <>
            {result.emailDelivery === "FAILED" ? (
              <Alert
                type="error"
                showIcon
                title="Chưa gửi được email"
                description={result.message || RESET_FAILED_MESSAGE}
              />
            ) : (
              <Alert
                type="success"
                showIcon
                title="Đã đặt lại mật khẩu"
                description="Mật khẩu tạm thời đã được gửi tới email cá nhân của nhân viên."
              />
            )}
            <Descriptions bordered column={1} size="small">
              <Descriptions.Item label="Email công ty (dùng để đăng nhập)">
                <Typography.Text code copyable>
                  {result.companyEmail}
                </Typography.Text>
              </Descriptions.Item>
              <Descriptions.Item label="Email cá nhân">
                {result.personalEmail}
              </Descriptions.Item>
              <Descriptions.Item label="Trạng thái gửi email">
                {result.emailDelivery === "SENT" ? "Đã gửi" : "Gửi thất bại"}
              </Descriptions.Item>
            </Descriptions>
          </>
        )}
        {!result && (
          <Form form={form} layout="vertical" onFinish={handleFinish} preserve={false}>
            <Form.Item
              label="Email cá nhân"
              name="personalEmail"
              rules={[
                { required: true, whitespace: true, message: "Nhập email cá nhân." },
                { type: "email", message: "Email cá nhân không hợp lệ." },
                { max: 150, message: "Email cá nhân tối đa 150 ký tự." },
                {
                  validator: (_, value) =>
                    !value ||
                    !value.trim().toLowerCase().endsWith("@smartpool.com")
                      ? Promise.resolve()
                      : Promise.reject(
                          new Error(
                            "Email công ty không dùng làm email cá nhân.",
                          ),
                        ),
                },
              ]}
            >
              <Input
                autoComplete="off"
                placeholder={loadingPrefill ? "Đang tải email đã lưu…" : ""}
                suffix={loadingPrefill ? <Spin size="small" /> : null}
              />
            </Form.Item>
            <EmailPrefill
              key={userId}
              form={form}
              open={open}
              next={storedEmail || loadedEmail}
              userId={userId}
            />
          </Form>
        )}
      </div>
    </Modal>
  );
}
