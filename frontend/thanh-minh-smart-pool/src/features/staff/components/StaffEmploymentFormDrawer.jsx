import { useEffect, useRef } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Button,
  DatePicker,
  Drawer,
  Empty,
  Form,
  Input,
  Modal,
  Select,
  Spin,
} from "antd";
import dayjs from "dayjs";
import {
  getStaffErrorMessage,
  useCreateStaff,
  useUpdateStaff,
} from "../hooks/useStaffMutations";
import { useStaffOptions } from "../hooks/useStaffs";
import staffService from "../services/staffService";

const statusOptions = [
  { label: "Đang làm việc", value: "Working" },
  { label: "Không hoạt động", value: "Inactive" },
];

const detailFields = [
  "fullName",
  "phone",
  "address",
  "dateOfBirth",
  "cccdNumber",
  "permanentAddress",
  "temporaryAddress",
  "bankName",
  "bankAccountNumber",
  "taxIdentifierKind",
  "taxIdentifier",
];

function detailValues(details = {}) {
  return {
    ...details,
    dateOfBirth: details.dateOfBirth ? dayjs(details.dateOfBirth) : undefined,
  };
}

function comparableValue(name, value) {
  if (name === "dateOfBirth") return value?.format?.("YYYY-MM-DD") || null;
  return typeof value === "string" ? value.trim() : (value ?? null);
}

function StaffEmploymentEditForm({ form, data, mutation, onFinish }) {
  useEffect(() => {
    form.setFieldsValue({ status: data.status, ...detailValues(data.details) });
  }, [data, form]);

  return (
    <Form form={form} layout="vertical" onFinish={onFinish} preserve={false}>
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
              onClick={() => form.submit()}
              disabled={mutation.isPending}
            >
              Thử lại
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
      <Alert
        type="info"
        showIcon
        title="Trạng thái Không hoạt động"
        description="Trạng thái này chặn công việc và chấm công vào, nhưng không khóa đăng nhập hoặc chấm công ra hợp lệ."
      />
      {data.detailsEditingBlocked && (
        <Alert
          type="error"
          showIcon
          title="Hồ sơ đang có yêu cầu chỉnh sửa"
          description="Hoàn tất yêu cầu hiện có trước khi sửa dữ liệu chi tiết."
        />
      )}
      <Form.Item
        label="Họ và tên"
        name="fullName"
        rules={[{ required: true, message: "Nhập họ và tên." }]}
      >
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item
        label="Số điện thoại"
        name="phone"
        rules={[{ required: true, message: "Nhập số điện thoại." }]}
      >
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item label="Địa chỉ liên hệ" name="address">
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item label="Ngày sinh" name="dateOfBirth">
        <DatePicker
          className="service-full-width"
          format="DD/MM/YYYY"
          disabled={data.detailsEditingBlocked}
        />
      </Form.Item>
      <Form.Item label="CCCD" name="cccdNumber">
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item label="Địa chỉ thường trú" name="permanentAddress">
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item label="Địa chỉ tạm trú" name="temporaryAddress">
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item label="Tên ngân hàng" name="bankName">
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item label="Số tài khoản ngân hàng" name="bankAccountNumber">
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item label="Loại mã số thuế" name="taxIdentifierKind">
        <Select
          allowClear
          disabled={data.detailsEditingBlocked}
          options={[
            { label: "CCCD", value: "PERSONAL_ID" },
            { label: "Mã số thuế", value: "LEGACY_TAX_CODE" },
          ]}
        />
      </Form.Item>
      <Form.Item label="Mã số thuế" name="taxIdentifier">
        <Input disabled={data.detailsEditingBlocked} />
      </Form.Item>
      <Form.Item
        label="Trạng thái việc làm"
        name="status"
        rules={[{ required: true, message: "Chọn trạng thái." }]}
      >
        <Select options={statusOptions} />
      </Form.Item>
    </Form>
  );
}

export default function StaffEmploymentFormDrawer({ open, onClose, staff }) {
  const [form] = Form.useForm();
  const submitting = useRef(false);
  const baseline = useRef(null);
  const queryClient = useQueryClient();
  const isEditing = Boolean(staff);
  const optionsQuery = useStaffOptions(open && !isEditing);
  const editQuery = useQuery({
    queryKey: ["staff-edit", staff?.userId],
    queryFn: ({ signal }) => staffService.getEdit(staff.userId, signal),
    enabled: open && isEditing,
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    retry: false,
  });
  const createMutation = useCreateStaff();
  const updateMutation = useUpdateStaff();
  const mutation = isEditing ? updateMutation : createMutation;
  const isPending = mutation.isPending;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    baseline.current = null;
    if (!isEditing)
      form.setFieldsValue({ joinDate: dayjs(), status: "Working" });
  }, [form, isEditing, open, staff?.userId]);

  useEffect(() => {
    if (!open || !isEditing || !editQuery.data) return;
    baseline.current = {
      status: editQuery.data.status,
      ...detailValues(editQuery.data.details),
    };
  }, [editQuery.data, form, isEditing, open]);

  const dismiss = () => {
    form.resetFields();
    baseline.current = null;
    if (staff?.userId) {
      queryClient.cancelQueries({ queryKey: ["staff-edit", staff.userId] });
      queryClient.removeQueries({ queryKey: ["staff-edit", staff.userId] });
    }
    mutation.reset();
    onClose();
  };

  const close = () => {
    if (isPending) return;
    const current = form.getFieldsValue();
    const dirty =
      baseline.current &&
      (current.status !== baseline.current.status ||
        detailFields.some(
          (name) =>
            comparableValue(name, current[name]) !==
            comparableValue(name, baseline.current[name]),
        ));
    if (isEditing && dirty) {
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
    if (
      submitting.current ||
      isPending ||
      (isEditing && (!editQuery.data || !baseline.current))
    )
      return;
    submitting.current = true;
    const data = { status: values.status };
    if (isEditing) {
      const details = {};
      const initial = baseline.current || {};
      for (const name of detailFields) {
        const next = comparableValue(name, values[name]);
        if (next === comparableValue(name, initial[name])) continue;
        details[name] = name === "address" && next === "" ? null : next;
      }
      if (Object.keys(details).length) {
        if (!editQuery.data) {
          submitting.current = false;
          return;
        }
        Object.assign(data, {
          details,
          expectedUpdatedAt: editQuery.data.expectedUpdatedAt,
          expectedUserUpdatedAt: editQuery.data.expectedUserUpdatedAt,
          expectedProfileUpdatedAt: editQuery.data.expectedProfileUpdatedAt,
          expectedProfileExists: editQuery.data.expectedProfileExists,
          expectedApprovedRevisionId: editQuery.data.expectedApprovedRevisionId,
          expectedApprovedRevisionVersion:
            editQuery.data.expectedApprovedRevisionVersion,
          expectedCorrectionVersion: editQuery.data.expectedCorrectionVersion,
        });
      }
      updateMutation.mutate(
        { userId: staff.userId, data },
        {
          onSuccess: dismiss,
          onSettled: () => {
            submitting.current = false;
          },
        },
      );
      return;
    }
    createMutation.mutate(
      {
        userId: values.userId,
        joinDate: values.joinDate.format("YYYY-MM-DD"),
        ...data,
      },
      {
        onSuccess: onClose,
        onSettled: () => {
          submitting.current = false;
        },
      },
    );
  };

  const users = optionsQuery.data?.users || [];
  const loadingEdit = isEditing && editQuery.isLoading;
  return (
    <Drawer
      rootClassName="staff-employment-drawer"
      title={isEditing ? "Cập nhật hồ sơ nhân viên" : "Thêm nhân viên"}
      open={open}
      onClose={close}
      size="default"
      forceRender
      closable={!isPending}
      mask={{ closable: !isPending }}
      extra={
        <Button
          type="primary"
          onClick={() => form.submit()}
          loading={isPending}
          disabled={
            isEditing && (loadingEdit || !editQuery.data || editQuery.isError)
          }
        >
          Lưu
        </Button>
      }
    >
      {optionsQuery.isLoading && !isEditing && (
        <div className="admin-management-state">
          <Spin size="large" />
        </div>
      )}
      {optionsQuery.isError && !isEditing && (
        <Empty description="Không thể tải tài khoản đủ điều kiện.">
          <Button onClick={optionsQuery.refetch}>Thử lại</Button>
        </Empty>
      )}
      {loadingEdit && (
        <div className="admin-management-state">
          <Spin size="large" />
        </div>
      )}
      {editQuery.isError && isEditing && (
        <Empty description="Không thể tải dữ liệu riêng tư của hồ sơ.">
          <Button onClick={editQuery.refetch}>Thử lại</Button>
        </Empty>
      )}
      {!isEditing && (
        <Form
          form={form}
          layout="vertical"
          onFinish={handleFinish}
          initialValues={{ joinDate: dayjs(), status: "Working" }}
          preserve={false}
        >
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
                  onClick={() => form.submit()}
                  disabled={isPending}
                >
                  Thử lại
                </Button>
              }
            />
          )}
          <Form.Item
            label="Tài khoản"
            name="userId"
            rules={[{ required: true, message: "Chọn tài khoản." }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder="Chọn tài khoản đủ điều kiện"
              options={users.map((user) => ({
                value: user.userId,
                label:
                  user.fullName || user.username || user.email || user.userId,
              }))}
            />
          </Form.Item>
          <Form.Item
            label="Ngày vào làm"
            name="joinDate"
            rules={[{ required: true, message: "Chọn ngày vào làm." }]}
          >
            <DatePicker className="service-full-width" format="DD/MM/YYYY" />
          </Form.Item>
          <Form.Item
            label="Trạng thái việc làm"
            name="status"
            rules={[{ required: true, message: "Chọn trạng thái." }]}
          >
            <Select options={statusOptions} />
          </Form.Item>
        </Form>
      )}
      {isEditing && editQuery.data && (
        <StaffEmploymentEditForm
          form={form}
          data={editQuery.data}
          mutation={mutation}
          onFinish={handleFinish}
        />
      )}
    </Drawer>
  );
}
