import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Avatar,
  Button,
  DatePicker,
  Drawer,
  Empty,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Spin,
  Typography,
  Upload,
  message,
} from "antd";
import { UploadOutlined } from "@ant-design/icons";
import dayjs from "dayjs";
import {
  getStaffErrorMessage,
  setStaffFormErrors,
  useUpdateStaff,
  validateStaffAvatarFile,
} from "../hooks/useStaffMutations";
import { STAFFS_QUERY_KEY } from "../hooks/useStaffs";
import staffService from "../services/staffService";
import {
  employeeAgeValidationMessage,
  isEmployeeBirthDateEligible,
  latestEmployeeBirthDate,
  phonePattern,
} from "../staffUi";
import { USER_ROLES } from "../../users/utils/userManagement";
import { buildProfilePayload, comparableValue, detailFields } from "../staffEditPayload";

const statusOptions = [
  { label: "Đang làm việc", value: "Working" },
  { label: "Không hoạt động", value: "Inactive" },
];

function detailValues(details = {}) {
  return {
    ...details,
    dateOfBirth: details.dateOfBirth ? dayjs(details.dateOfBirth) : undefined,
  };
}

function StaffEmploymentEditForm({ form, data, mutation, onFinish, avatarSection, staff, onReload }) {
  const birthDateCutoff = latestEmployeeBirthDate();
  const initial = detailValues(data.details);
  const changed = (name, value) => comparableValue(name, value) !== comparableValue(name, initial[name]);

  useEffect(() => {
    form.setFieldsValue(detailValues(data.details));
  }, [data, form]);

  return (
    <Form form={form} layout="vertical" className="staff-contract-stack" onFinish={onFinish} preserve={false}>
      {avatarSection}
      {mutation.error && (
        <Alert
          type="error"
          showIcon
          title="Không thể lưu hồ sơ nhân viên"
          description={getStaffErrorMessage(
            mutation.error,
            "Máy chủ không thể lưu hồ sơ nhân viên.",
          )}
          action={
            <Button
              size="small"
              onClick={mutation.error?.response?.data?.code === "STALE_VERSION" ? onReload : () => form.submit()}
              disabled={mutation.isPending}
            >
              {mutation.error?.response?.data?.code === "STALE_VERSION" ? "Tải lại hồ sơ" : "Thử lại"}
            </Button>
          }
        />
      )}
      <Alert
        type="warning"
        showIcon
        title="Lưu trực tiếp"
        description="Thay đổi ảnh hưởng dữ liệu nhận diện và thanh toán hiện tại. Nhân viên không cần gửi lại hồ sơ."
      />
      <section><h3 className="staff-form-section-heading">Thông tin tài khoản</h3>
        <Typography.Paragraph>Email công ty: {staff?.companyEmail || "Chưa có email công ty"}<br />Vai trò: {USER_ROLES.find((item) => item.value === staff?.roleName?.toUpperCase())?.label || staff?.roleName || "Chưa có vai trò"}<br />Ngày vào làm: {staff?.joinDate ? dayjs(staff.joinDate).format("DD/MM/YYYY") : "Chưa có thông tin"}</Typography.Paragraph>
      </section>
      <section><h3 className="staff-form-section-heading">Thông tin cá nhân</h3><div className="staff-form-section-grid">
      <Form.Item
        label="Họ và tên"
        name="fullName"
        rules={[{ max: 255, message: "Họ và tên tối đa 255 ký tự." }, { validator: (_, value) => !changed("fullName", value) || value?.trim() ? Promise.resolve() : Promise.reject(new Error("Nhập họ và tên.")) }]}
      >
        <Input />
      </Form.Item>
      <Form.Item
        label="Số điện thoại"
        name="phone"
        rules={[{ validator: (_, value) => !changed("phone", value) || phonePattern.test(value || "") ? Promise.resolve() : Promise.reject(new Error("Nhập số di động Việt Nam hợp lệ.")) }]}
      >
        <Input />
      </Form.Item>
      <Form.Item label="Địa chỉ liên hệ" name="address">
        <Input />
      </Form.Item>
      <Form.Item
        label="Ngày sinh"
        name="dateOfBirth"
        rules={[
          {
            validator: (_, value) =>
              !changed("dateOfBirth", value) || (value && isEmployeeBirthDateEligible(value))
                ? Promise.resolve()
                : Promise.reject(new Error(employeeAgeValidationMessage)),
          },
        ]}
      >
        <DatePicker
          className="service-full-width"
          format="DD/MM/YYYY"
          placeholder="Chọn ngày sinh"
          defaultPickerValue={birthDateCutoff}
          disabledDate={(date) => date.isAfter(birthDateCutoff, "day")}
        />
      </Form.Item>
      <Form.Item label="Giới tính" name="gender" rules={[{ validator: (_, value) => !changed("gender", value) || ["MALE", "FEMALE"].includes(value) ? Promise.resolve() : Promise.reject(new Error("Chọn giới tính.")) }]}><Select options={[{ label: "Nam", value: "MALE" }, { label: "Nữ", value: "FEMALE" }]} /></Form.Item>
      <Form.Item label="Email cá nhân" name="personalEmail" rules={[{ validator: (_, value) => !changed("personalEmail", value) || (value && value.length <= 150 && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim()) && !value.trim().toLowerCase().endsWith("@smartpool.com")) ? Promise.resolve() : Promise.reject(new Error("Nhập email cá nhân hợp lệ, không dùng email công ty.")) }]}><Input autoComplete="email" maxLength={150} /></Form.Item>
      </div></section>
      <section><h3 className="staff-form-section-heading">Thông tin pháp lý</h3><div className="staff-form-section-grid">
      <Form.Item label="Số CCCD" name="cccdNumber" rules={[{ validator: (_, value) => !changed("cccdNumber", value) || /^[0-9]{12}$/.test(value || "") ? Promise.resolve() : Promise.reject(new Error("Số CCCD phải gồm 12 chữ số.")) }]}>
        <Input inputMode="numeric" maxLength={12} />
      </Form.Item>
      <Form.Item label="Địa chỉ thường trú" name="permanentAddress" rules={[{ max: 500 }, { validator: (_, value) => !changed("permanentAddress", value) || value?.trim() ? Promise.resolve() : Promise.reject(new Error("Nhập địa chỉ thường trú.")) }]}>
        <Input />
      </Form.Item>
      <Form.Item label="Địa chỉ tạm trú" name="temporaryAddress">
        <Input />
      </Form.Item>
      <Form.Item label="Tên ngân hàng" name="bankName">
        <Input />
      </Form.Item>
      <Form.Item label="Số tài khoản ngân hàng" name="bankAccountNumber">
        <Input />
      </Form.Item>
      <Form.Item label="Loại mã số thuế" name="taxIdentifierKind">
        <Select
          allowClear
          options={[
            { label: "CCCD", value: "PERSONAL_ID" },
            { label: "Mã số thuế", value: "LEGACY_TAX_CODE" },
          ]}
        />
      </Form.Item>
      <Form.Item label="Mã số thuế" name="taxIdentifier">
        <Input />
      </Form.Item>
      </div></section>
    </Form>
  );
}

export default function StaffEmploymentFormDrawer({ open, onClose, staff }) {
  const [form] = Form.useForm();
  const submitting = useRef(false);
  const baseline = useRef(null);
  const snapshot = useRef(null);
  const queryClient = useQueryClient();
  const userId = staff?.userId;
  const [status, setStatus] = useState();
  const [savedStatus, setSavedStatus] = useState();
  const [statusError, setStatusError] = useState("");
  const [statusPending, setStatusPending] = useState(false);
  const [snapshotError, setSnapshotError] = useState("");
  const [avatarFile, setAvatarFile] = useState(null);
  const [avatarStatus, setAvatarStatus] = useState("idle");
  const [avatarError, setAvatarError] = useState("");
  const editQuery = useQuery({
    queryKey: ["staff-edit", userId],
    queryFn: ({ signal }) => staffService.getEdit(userId, signal),
    enabled: open && Boolean(userId),
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    retry: false,
  });
  const updateMutation = useUpdateStaff();
  const isPending = updateMutation.isPending;
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

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    baseline.current = null;
    snapshot.current = null;
  }, [form, open, userId]);

  useEffect(() => {
    if (!open || !editQuery.data) return;
    baseline.current = {
      status: editQuery.data.status,
      ...detailValues(editQuery.data.details),
    };
    snapshot.current = editQuery.data;
  }, [editQuery.data, form, open]);

  const dismiss = () => {
    form.resetFields();
    baseline.current = null;
    if (userId) {
      queryClient.cancelQueries({ queryKey: ["staff-edit", userId] });
      queryClient.removeQueries({ queryKey: ["staff-edit", userId] });
    }
    updateMutation.reset();
    setAvatarFile(null);
    setAvatarStatus("idle");
    setAvatarError("");
    onClose();
  };

  const close = () => {
    if (isPending || statusPending || avatarStatus === "uploading") return;
    const current = form.getFieldsValue();
    const dirty =
      baseline.current &&
      (detailFields.some(
          (name) =>
            comparableValue(name, current[name]) !==
            comparableValue(name, baseline.current[name]),
        ));
    if (dirty) {
      Modal.confirm({
        title: "Bỏ thay đổi chưa lưu?",
        content: "Các thay đổi hồ sơ sẽ không được lưu.",
        okText: "Bỏ thay đổi",
        okButtonProps: { danger: true },
        cancelText: "Tiếp tục chỉnh sửa",
        onOk: dismiss,
      });
      return;
    }
    dismiss();
  };

  const handleFinish = (values) => {
    if (submitting.current || isPending || statusPending || avatarStatus === "uploading" || snapshotError || !editQuery.data || !baseline.current || !snapshot.current)
      return;
    submitting.current = true;
    const data = buildProfilePayload(values, baseline.current, snapshot.current);
    if (!data) { submitting.current = false; dismiss(); return; }
    updateMutation.mutate(
      { userId, data },
      {
        onSuccess: dismiss,
        onError: (failure) => setStaffFormErrors(form, failure),
        onSettled: () => {
          submitting.current = false;
        },
      },
    );
  };

  const refreshSnapshot = async (allowedTimestamp) => {
    const previous = snapshot.current;
    const latest = await staffService.getEdit(userId);
    const unchanged = previous && JSON.stringify(previous.details) === JSON.stringify(latest.details) &&
      ["expectedUpdatedAt", "expectedUserUpdatedAt", "expectedProfileUpdatedAt", "expectedProfileExists", "expectedLegalProfileVersion"]
        .every((name) => name === allowedTimestamp || previous[name] === latest[name]);
    if (!unchanged) throw new Error("Hồ sơ đã thay đổi. Hãy tải lại trước khi lưu.");
    snapshot.current = latest;
  };

  const uploadAvatar = async (file) => {
    if (!userId || avatarStatus === "uploading") return;
    setAvatarStatus("uploading");
    setAvatarError("");
    try {
      await staffService.uploadAvatar(userId, file);
      try { await refreshSnapshot("expectedProfileUpdatedAt"); }
      catch { setSnapshotError("Ảnh đã được tải. Hãy tải lại hồ sơ trước khi lưu thay đổi."); }
      setAvatarStatus("done");
      queryClient.invalidateQueries({ queryKey: STAFFS_QUERY_KEY });
    } catch (failure) {
      setAvatarStatus("failed");
      setAvatarError(
        getStaffErrorMessage(
          failure,
          "Tải ảnh đại diện thất bại. Hồ sơ nhân viên vẫn được giữ; thử lại chỉ phần tải ảnh.",
        ),
      );
    }
  };

  const pickAvatar = (file) => {
    const validationError = validateStaffAvatarFile(file);
    if (validationError) {
      message.error(validationError);
      return Upload.LIST_IGNORE;
    }
    setAvatarFile(file);
    uploadAvatar(file);
    return false;
  };

  const loadingEdit = editQuery.isLoading;
  const avatarSection = editQuery.data ? (
    <div className="staff-contract-stack">
      <Typography.Text strong>Ảnh đại diện</Typography.Text>
      <Space wrap>
        {(avatarPreview || staff?.avatarUrl) && (
          <Avatar
            src={avatarPreview || staff.avatarUrl}
            size={64}
            alt="Ảnh đại diện nhân viên"
          />
        )}
        <Upload
          accept="image/jpeg,image/png"
          maxCount={1}
          showUploadList={false}
          beforeUpload={pickAvatar}
        >
          <Button
            icon={<UploadOutlined />}
            loading={avatarStatus === "uploading"}
            disabled={isPending}
          >
            Chọn ảnh
          </Button>
        </Upload>
        {avatarFile && (
          <Typography.Text type="secondary">
            {avatarFile.name}
          </Typography.Text>
        )}
      </Space>
      {avatarStatus === "done" && (
        <Alert
          type="success"
          showIcon
          title="Đã tải ảnh đại diện"
          description="Ảnh đại diện đã được cập nhật."
        />
      )}
      {avatarError && (
        <Alert
          type="error"
          showIcon
          title="Chưa tải được ảnh đại diện"
          description={avatarError}
          action={
            <Button
              size="small"
              onClick={() => uploadAvatar(avatarFile)}
              disabled={avatarStatus === "uploading" || !avatarFile}
            >
              Thử lại
            </Button>
          }
        />
      )}
    </div>
  ) : null;
  return (
    <Drawer
      rootClassName="staff-employment-drawer"
      title="Cập nhật hồ sơ nhân viên"
      open={open}
      onClose={close}
      size="840"
      forceRender
      closable={!isPending && !statusPending && avatarStatus !== "uploading"}
      mask={{ closable: !isPending && !statusPending && avatarStatus !== "uploading" }}
      extra={
        <Space>
          <Button
            type="primary"
            onClick={() => form.submit()}
            loading={isPending}
            disabled={loadingEdit || !editQuery.data || editQuery.isError || statusPending || avatarStatus === "uploading" || Boolean(snapshotError)}
          >
            Lưu
          </Button>
        </Space>
      }
    >
      {loadingEdit && (
        <div className="admin-management-state">
          <Spin size="large" />
        </div>
      )}
      {editQuery.isError && (
        <Empty description="Không thể tải dữ liệu riêng tư của hồ sơ.">
          <Button onClick={editQuery.refetch}>Thử lại</Button>
        </Empty>
      )}
      {snapshotError && <Alert type="warning" showIcon title="Cần tải lại hồ sơ" description={snapshotError} action={<Button size="small" onClick={() => Modal.confirm({ title: "Tải lại hồ sơ?", content: "Các thay đổi chưa lưu sẽ bị bỏ.", okText: "Tải lại", cancelText: "Hủy", onOk: async () => { const result = await editQuery.refetch(); if (result.isSuccess) setSnapshotError(""); } })}>Tải lại</Button>} />}
      {editQuery.data && (
        <StaffEmploymentEditForm
          form={form}
          data={editQuery.data}
          mutation={updateMutation}
          onFinish={handleFinish}
          avatarSection={avatarSection}
          staff={staff}
          onReload={() => Modal.confirm({ title: "Tải lại hồ sơ?", content: "Các thay đổi chưa lưu sẽ bị bỏ.", okText: "Tải lại", cancelText: "Hủy", onOk: () => { updateMutation.reset(); editQuery.refetch(); } })}
        />
      )}
      {editQuery.data && <section className="staff-contract-stack" aria-label="Thay đổi trạng thái việc làm">
        <h3 className="staff-form-section-heading">Trạng thái việc làm</h3>
        <Alert type="info" showIcon title="Thao tác riêng" description="Không hoạt động sẽ khóa tài khoản. Đang làm việc sẽ mở lại quyền đăng nhập thủ công." />
        {statusError && <Alert type="error" showIcon title="Không thể đổi trạng thái" description={statusError} />}
        <Space wrap><Select aria-label="Trạng thái việc làm" value={status ?? editQuery.data.status} onChange={setStatus} options={statusOptions} style={{ minWidth: 180 }} /><Button disabled={!status || status === (savedStatus ?? editQuery.data.status) || isPending || avatarStatus === "uploading" || Boolean(snapshotError)} loading={statusPending} onClick={() => Modal.confirm({ title: "Thay đổi trạng thái việc làm?", content: "Thao tác này có thể thay đổi quyền đăng nhập của nhân viên.", okText: "Xác nhận", cancelText: "Hủy", onOk: async () => { setStatusPending(true); setStatusError(""); try { await staffService.update(userId, { status }); setSavedStatus(status); try { await refreshSnapshot("expectedUpdatedAt"); } catch { setSnapshotError("Trạng thái đã được cập nhật. Hãy tải lại hồ sơ trước khi lưu thay đổi."); } queryClient.invalidateQueries({ queryKey: STAFFS_QUERY_KEY }); } catch (error) { setStatusError(getStaffErrorMessage(error, "Hãy thử lại.")); } finally { setStatusPending(false); } } })}>Cập nhật trạng thái</Button></Space>
      </section>}
    </Drawer>
  );
}
