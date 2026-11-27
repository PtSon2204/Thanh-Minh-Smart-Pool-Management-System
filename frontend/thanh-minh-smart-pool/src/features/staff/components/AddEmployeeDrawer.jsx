import { useEffect, useMemo, useRef, useState } from "react";
import {
  Alert,
  Avatar,
  Button,
  DatePicker,
  Descriptions,
  Drawer,
  Form,
  Input,
  Select,
  Space,
  Spin,
  Typography,
  Upload,
  message,
} from "antd";
import { UploadOutlined } from "@ant-design/icons";
import dayjs from "dayjs";
import ContractTermsFields from "./ContractTermsFields";
import StaffPasswordResetModal from "./StaffPasswordResetModal";
import {
  getStaffErrorMessage,
  setStaffFormErrors,
  useCreateStaff,
  validateStaffAvatarFile,
} from "../hooks/useStaffMutations";
import { useActiveShifts } from "../hooks/useShifts";
import staffService from "../services/staffService";
import {
  employeeAgeValidationMessage,
  invitationRoleOptions,
  isEmployeeBirthDateEligible,
  latestEmployeeBirthDate,
  normalizePhone,
  phonePattern,
  vietnamToday,
} from "../staffUi";

const genderOptions = [
  { label: "Nam", value: "MALE" },
  { label: "Nữ", value: "FEMALE" },
];

export default function AddEmployeeDrawer({ open, onClose }) {
  const [form] = Form.useForm();
  const submitting = useRef(false);
  const [avatarFile, setAvatarFile] = useState(null);
  const [avatarStatus, setAvatarStatus] = useState("idle");
  const [result, setResult] = useState(null);
  const [createdEmail, setCreatedEmail] = useState("");
  const [resetOpen, setResetOpen] = useState(false);
  const createMutation = useCreateStaff();
  const shiftsQuery = useActiveShifts(open);
  const shifts = shiftsQuery.data?.items || [];
  const birthDateCutoff = latestEmployeeBirthDate();
  const isPending = createMutation.isPending;
  const avatarPreview = useMemo(
    () => (avatarFile ? URL.createObjectURL(avatarFile) : ""),
    [avatarFile],
  );

  useEffect(
    () => () => {
      if (avatarPreview) URL.revokeObjectURL(avatarPreview);
    },
    [avatarPreview],
  );

  const close = () => {
    if (isPending || submitting.current) return;
    form.resetFields();
    setAvatarFile(null);
    setAvatarStatus("idle");
    setResult(null);
    setCreatedEmail("");
    setResetOpen(false);
    createMutation.reset();
    onClose();
  };

  const handleFinish = (values) => {
    if (submitting.current || isPending || result) return;
    submitting.current = true;
    const joinDate = values.joinDate.format("YYYY-MM-DD");
    const payload = {
      fullName: values.fullName.trim(),
      gender: values.gender,
      dateOfBirth: values.dateOfBirth.format("YYYY-MM-DD"),
      phone: normalizePhone(values.phone),
      personalEmail: values.personalEmail.trim().toLowerCase(),
      cccdNumber: values.cccdNumber.trim(),
      permanentAddress: values.permanentAddress.trim(),
      temporaryAddress: values.temporaryAddress?.trim() || null,
      joinDate,
      targetRole: values.targetRole,
      contract: {
        ...values.contract,
        startDate: joinDate,
      },
    };
    createMutation.mutate(payload, {
      onSuccess: async (data) => {
        setResult(data);
        setCreatedEmail(payload.personalEmail);
        if (avatarFile && data.userId) {
          setAvatarStatus("uploading");
          try {
            await staffService.uploadAvatar(data.userId, avatarFile);
            setAvatarStatus("done");
          } catch {
            setAvatarStatus("failed");
          }
        }
      },
      onError: (failure) => {
        setStaffFormErrors(form, failure);
      },
      onSettled: () => {
        submitting.current = false;
      },
    });
  };

  const pickAvatar = (file) => {
    const validationError = validateStaffAvatarFile(file);
    if (validationError) {
      setAvatarFile(null);
      message.error(validationError);
      return Upload.LIST_IGNORE;
    }
    setAvatarFile(file);
    setAvatarStatus("idle");
    return false;
  };

  return (
    <Drawer
      rootClassName="staff-employment-drawer"
      title="Thêm nhân viên"
      open={open}
      onClose={close}
      size="840"
      destroyOnHidden
      closable={!isPending}
      mask={{ closable: !isPending }}
      extra={
        result ? (
          <Button type="primary" onClick={close}>
            Hoàn tất
          </Button>
        ) : (
          <Button
            type="primary"
            onClick={() => form.submit()}
            loading={isPending}
          >
            Thêm nhân viên
          </Button>
        )
      }
    >
      {result ? (
        <div className="staff-contract-stack">
          <Alert
            type="success"
            showIcon
            title="Đã tạo nhân viên"
            description="Tài khoản nhân viên và hợp đồng ban đầu đã được tạo. Mật khẩu tạm thời được gửi tới email cá nhân."
          />
          {result.emailDelivery === "FAILED" && (
            <Alert
              type="warning"
              showIcon
              title="Email chưa gửi được"
              description="Đã tạo nhân viên nhưng chưa gửi được email. Hãy đặt lại mật khẩu để gửi lại."
              action={
                <Button size="small" onClick={() => setResetOpen(true)}>
                  Đặt lại mật khẩu
                </Button>
              }
            />
          )}
          <Descriptions bordered column={1} size="small">
            <Descriptions.Item label="Email công ty (dùng để đăng nhập)">
              <Typography.Text code copyable>
                {result.companyEmail}
              </Typography.Text>
            </Descriptions.Item>
            <Descriptions.Item label="Email cá nhân">
              {createdEmail}
            </Descriptions.Item>
            <Descriptions.Item label="Trạng thái gửi email">
              {result.emailDelivery === "SENT" ? "Đã gửi" : "Gửi thất bại"}
            </Descriptions.Item>
          </Descriptions>
          {avatarFile && avatarStatus === "uploading" && (
            <Space>
              <Spin size="small" />
              <span>Đang tải ảnh đại diện…</span>
            </Space>
          )}
          {avatarFile && avatarStatus === "done" && (
            <Alert
              type="success"
              showIcon
              title="Đã tải ảnh đại diện"
              description="Ảnh đại diện đã được lưu cho nhân viên vừa tạo."
            />
          )}
          {avatarFile && avatarStatus === "failed" && (
            <Alert
              type="warning"
              showIcon
              title="Chưa tải được ảnh đại diện"
              description="Nhân viên đã được tạo. Mở Sửa hồ sơ để thử lại chỉ phần tải ảnh."
            />
          )}
        </div>
      ) : (
        <Form
          form={form}
          layout="vertical"
          className="staff-contract-stack"
          onFinish={handleFinish}
          initialValues={{
            joinDate: dayjs(vietnamToday()),
            contract: {
              employmentType: "FULL_TIME",
              startDate: vietnamToday(),
              weeklyPattern: Array.from({ length: 7 }, (_, index) => ({
                dayOfWeek: index + 1,
                shiftIds: [],
              })),
            },
          }}
          onValuesChange={(changed) => {
            if (changed.joinDate)
              form.setFieldValue(
                ["contract", "startDate"],
                changed.joinDate.format("YYYY-MM-DD"),
              );
          }}
          disabled={isPending || shiftsQuery.isLoading || shiftsQuery.isError}
        >
          <Alert
            type="info"
            showIcon
            title="Thông tin đăng nhập"
            description="Thông tin đăng nhập sẽ được gửi tới email cá nhân. Nhân viên phải đổi mật khẩu sau lần đăng nhập đầu tiên."
          />
          {createMutation.error && (
            <Alert
              type="error"
              showIcon
              title="Không thể tạo nhân viên"
              description={getStaffErrorMessage(
                createMutation.error,
                "Máy chủ không thể tạo nhân viên.",
              )}
            />
          )}
          {shiftsQuery.isError && (
            <Alert
              type="error"
              showIcon
              title="Không thể tải ca làm việc"
              description="Thử tải lại trước khi thêm nhân viên."
              action={
                <Button size="small" onClick={() => shiftsQuery.refetch()}>
                  Thử lại
                </Button>
              }
            />
          )}
          <section aria-label="Thông tin cá nhân">
            <h3 className="staff-form-section-heading">Thông tin cá nhân</h3>
            <div className="staff-form-section-grid staff-add-personal-grid">
              <div className="staff-form-section-column">
                <Form.Item
                  name="fullName"
                  label="Họ và tên"
                  rules={[
                    {
                      required: true,
                      whitespace: true,
                      message: "Nhập họ và tên.",
                    },
                    { max: 255, message: "Họ và tên tối đa 255 ký tự." },
                  ]}
                >
                  <Input maxLength={255} autoComplete="name" />
                </Form.Item>
                <Form.Item
                  name="gender"
                  label="Giới tính"
                  rules={[{ required: true, message: "Chọn giới tính." }]}
                >
                  <Select
                    placeholder="Chọn giới tính"
                    options={genderOptions}
                    className="service-full-width"
                  />
                </Form.Item>
                <Form.Item
                  name="phone"
                  label="Điện thoại"
                  rules={[
                    {
                      required: true,
                      whitespace: true,
                      message: "Nhập số điện thoại.",
                    },
                    {
                      pattern: phonePattern,
                      message:
                        "Nhập số di động Việt Nam, bắt đầu bằng 0 hoặc +84.",
                    },
                  ]}
                >
                  <Input inputMode="tel" autoComplete="tel" />
                </Form.Item>
                <Form.Item
                  name="permanentAddress"
                  label="Địa chỉ thường trú"
                  rules={[
                    {
                      required: true,
                      whitespace: true,
                      message: "Nhập địa chỉ thường trú.",
                    },
                    {
                      max: 500,
                      message: "Địa chỉ thường trú tối đa 500 ký tự.",
                    },
                  ]}
                >
                  <Input.TextArea
                    maxLength={500}
                    autoSize={{ minRows: 2, maxRows: 3 }}
                  />
                </Form.Item>
              </div>
              <div className="staff-form-section-column">
                <Form.Item
                  name="cccdNumber"
                  label="Số CCCD"
                  rules={[
                    { required: true, message: "Nhập số CCCD." },
                    {
                      pattern: /^[0-9]{12}$/,
                      message: "Số CCCD phải gồm 12 chữ số.",
                    },
                  ]}
                >
                  <Input inputMode="numeric" maxLength={12} />
                </Form.Item>
                <Form.Item
                  name="dateOfBirth"
                  label="Ngày sinh"
                  rules={[
                    { required: true, message: "Nhập ngày sinh." },
                    {
                      validator: (_, value) =>
                        isEmployeeBirthDateEligible(value)
                          ? Promise.resolve()
                          : Promise.reject(
                              new Error(employeeAgeValidationMessage),
                            ),
                    },
                  ]}
                >
                  <DatePicker
                    format="DD/MM/YYYY"
                    className="service-full-width"
                    placeholder="Chọn ngày sinh"
                    defaultPickerValue={birthDateCutoff}
                    disabledDate={(date) =>
                      date.isAfter(birthDateCutoff, "day")
                    }
                  />
                </Form.Item>
                <Form.Item
                  name="personalEmail"
                  label="Email cá nhân"
                  rules={[
                    {
                      required: true,
                      type: "email",
                      message: "Nhập email cá nhân hợp lệ.",
                    },
                    { max: 150, message: "Email cá nhân tối đa 150 ký tự." },
                    {
                      validator: (_, value) =>
                        !value ||
                        !value.trim().toLowerCase().endsWith("@smartpool.com")
                          ? Promise.resolve()
                          : Promise.reject(
                              new Error(
                                "Email công ty chỉ do hệ thống tạo. Dùng email cá nhân khác.",
                              ),
                            ),
                    },
                  ]}
                >
                  <Input autoComplete="off" />
                </Form.Item>
                <Form.Item
                  name="temporaryAddress"
                  label="Địa chỉ tạm trú"
                  rules={[
                    { max: 500, message: "Địa chỉ tạm trú tối đa 500 ký tự." },
                  ]}
                >
                  <Input.TextArea
                    maxLength={500}
                    autoSize={{ minRows: 2, maxRows: 3 }}
                  />
                </Form.Item>
              </div>
            </div>
          </section>
          <section aria-label="Thông tin làm việc">
            <h3 className="staff-form-section-heading">Thông tin làm việc</h3>
            <div className="onboarding-form-grid">
              <Form.Item
                name="joinDate"
                label="Ngày vào làm"
                rules={[
                  { required: true, message: "Chọn ngày vào làm." },
                  {
                    validator: (_, value) =>
                      !value ||
                      value.format("YYYY-MM-DD") >= vietnamToday()
                        ? Promise.resolve()
                        : Promise.reject(
                            new Error("Ngày vào làm phải từ hôm nay trở đi."),
                          ),
                  },
                ]}
              >
                <DatePicker
                  format="DD/MM/YYYY"
                  className="service-full-width"
                  disabledDate={(date) =>
                    date.format("YYYY-MM-DD") < vietnamToday()
                  }
                />
              </Form.Item>
              <Form.Item
                name="targetRole"
                label="Vai trò"
                rules={[{ required: true, message: "Chọn vai trò." }]}
              >
                <Select
                options={invitationRoleOptions}
                className="service-full-width"
              />
              </Form.Item>
            </div>
          </section>
          {shiftsQuery.isLoading && (
            <div className="admin-management-state">
              <Spin size="large" />
            </div>
          )}
          <section aria-label="Hợp đồng và lịch làm việc">
            <h3 className="staff-form-section-heading">Hợp đồng và lịch làm việc</h3>
            <ContractTermsFields
              form={form}
              shifts={shifts}
              invitation
              compact
              showTitle={false}
              hideAlert
              hideStartDate
              hideWeeklyPattern
              disabled={
                isPending || shiftsQuery.isLoading || shiftsQuery.isError
              }
            />
            <ContractTermsFields
              form={form}
              shifts={shifts}
              invitation
              compact
              showTitle={false}
              hideAlert
              hideEmploymentType
              hideStartDate
              hideEndDate
              disabled={
                isPending || shiftsQuery.isLoading || shiftsQuery.isError
              }
            />
          </section>
          <section aria-label="Ảnh đại diện">
            <h3 className="staff-form-section-heading">Ảnh đại diện</h3>
            <Form.Item label="Ảnh đại diện (tùy chọn - JPG hoặc PNG, tối đa 5MB)">
              <Space wrap>
                {avatarPreview && (
                  <Avatar
                    src={avatarPreview}
                    size={64}
                    alt="Ảnh đại diện đã chọn"
                  />
                )}
                <Upload
                  accept="image/jpeg,image/png"
                  maxCount={1}
                  showUploadList={false}
                  beforeUpload={pickAvatar}
                >
                  <Button icon={<UploadOutlined />} disabled={isPending}>
                    Chọn ảnh
                  </Button>
                </Upload>
                {avatarFile && (
                  <Typography.Text type="secondary">
                    {avatarFile.name}
                  </Typography.Text>
                )}
              </Space>
            </Form.Item>
          </section>
        </Form>
      )}
      <StaffPasswordResetModal
        open={resetOpen && Boolean(result)}
        staff={result ? { userId: result.userId, email: createdEmail } : null}
        onClose={() => setResetOpen(false)}
      />
    </Drawer>
  );
}
