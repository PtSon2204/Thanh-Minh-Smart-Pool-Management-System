import { useEffect, useState, useMemo } from 'react';
import {
  Modal,
  Form,
  DatePicker,
  Select,
  Input,
  Space,
  Tag,
  Button,
  Alert,
  message,
} from 'antd';
import {
  EditOutlined,
  ExperimentOutlined,
  CheckCircleOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { waterQualityService } from '../services/waterQualityService';

const STATUS_OPTIONS = [
  { value: 'Scheduled', label: 'Đã phân công (Chờ trực)' },
  { value: 'InProgress', label: 'Đang trực bể' },
  { value: 'Completed', label: 'Đã hoàn thành trực & đo nước' },
  { value: 'Absent', label: 'Vắng mặt' },
  { value: 'Cancelled', label: 'Hủy phân công' },
];

export default function UpdatePoolDutyModal({
  visible,
  dutyItem,
  options,
  onCancel,
  onSuccess,
  onOpenCreateMeasurement,
}) {
  const [form] = Form.useForm();
  const [submitting, setSubmitting] = useState(false);

  const shifts = useMemo(() => options?.shifts || [], [options?.shifts]);
  const staffs = useMemo(() => options?.staffs || [], [options?.staffs]);
  const isEditing = Boolean(dutyItem?.id);

  useEffect(() => {
    if (visible && dutyItem) {
      form.setFieldsValue({
        dutyDate: dutyItem.dutyDate ? dayjs(dutyItem.dutyDate) : dayjs(),
        shiftId: dutyItem.shiftId || shifts[0]?.shiftId,
        assignedEmployeeId: dutyItem.assignedEmployeeId || staffs[0]?.employeeId,
        status: dutyItem.status || 'Scheduled',
        notes:
          dutyItem.notes ??
          'Đo pH, Clo tự do đầu ca và kiểm tra vệ sinh mặt nước bể bơi',
      });
    } else if (visible) {
      form.resetFields();
    }
  }, [visible, dutyItem, form, shifts, staffs]);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setSubmitting(true);

      const payload = {
        dutyDate: values.dutyDate.format('YYYY-MM-DD'),
        shiftId: values.shiftId,
        assignedEmployeeId: values.assignedEmployeeId,
        status: values.status,
        notes: values.notes?.trim() || null,
        waterQualityLogId: dutyItem?.waterQualityLogId || null,
      };

      const res = isEditing
        ? await waterQualityService.updateDutySchedule(dutyItem.id, payload)
        : await waterQualityService.saveDutySchedule(payload);

      message.success(
        isEditing
          ? 'Đã cập nhật lịch trực bể thành công!'
          : 'Đã phân công lịch trực bể thành công!'
      );
      onSuccess(res);
      onCancel();
    } catch (error) {
      if (error?.response?.data?.message) {
        message.error(error.response.data.message);
      } else if (!error?.errorFields) {
        message.error('Không thể lưu lịch trực bể.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      title={
        <Space>
          <EditOutlined style={{ color: '#1890ff', fontSize: 18 }} />
          <span>
            {isEditing
              ? 'Cập nhật lịch trực bể & Kiểm tra nước'
              : 'Phân công ca trực bể mới'}
          </span>
        </Space>
      }
      open={visible}
      onCancel={onCancel}
      footer={[
        onOpenCreateMeasurement && (
          <Button
            key="measure"
            icon={<ExperimentOutlined />}
            onClick={() => {
              onCancel();
              onOpenCreateMeasurement(dutyItem);
            }}
          >
            Nhập phiếu đo nước cho ca này
          </Button>
        ),
        <Button key="cancel" onClick={onCancel}>
          Đóng
        </Button>,
        <Button
          key="submit"
          type="primary"
          loading={submitting}
          onClick={handleSubmit}
        >
          {isEditing ? 'Lưu cập nhật' : 'Lưu phân công'}
        </Button>,
      ]}
      width={560}
      destroyOnClose
    >
      {dutyItem?.waterQualityLogId && (
        <Alert
          type="success"
          showIcon
          icon={<CheckCircleOutlined />}
          style={{ marginBottom: 16 }}
          message="Ca trực này đã hoàn thành đo chất lượng nước"
          description={
            <span>
              Chỉ số ghi nhận: <strong>pH {Number(dutyItem.measuredPh).toFixed(2)}</strong> —{' '}
              <strong>Clo {Number(dutyItem.measuredChlorine).toFixed(2)} mg/L</strong>{' '}
              <Tag color="green" style={{ marginLeft: 6 }}>
                {dutyItem.measurementStatus === 'WithinLimits'
                  ? 'Đạt chuẩn'
                  : dutyItem.measurementStatus}
              </Tag>
            </span>
          }
        />
      )}

      <Form form={form} layout="vertical">
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
          <Form.Item
            name="dutyDate"
            label="Ngày trực bể"
            rules={[{ required: true, message: 'Vui lòng chọn ngày trực' }]}
            tooltip={isEditing ? 'Ngày trực bể không được phép chỉnh sửa sau khi đã phân công' : undefined}
          >
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} disabled={isEditing} />
          </Form.Item>

          <Form.Item
            name="shiftId"
            label="Ca trực"
            rules={[{ required: true, message: 'Vui lòng chọn ca trực' }]}
          >
            <Select
              placeholder="Chọn ca trực"
              options={shifts.map((s) => ({
                value: s.shiftId,
                label: `${s.name} (${String(s.startTime).slice(0, 5)} - ${String(s.endTime).slice(0, 5)})`,
              }))}
            />
          </Form.Item>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
          <Form.Item
            name="assignedEmployeeId"
            label="Nhân viên phụ trách trực bể"
            rules={[{ required: true, message: 'Vui lòng chọn nhân viên trực' }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              placeholder="Chọn nhân viên trực bể"
              options={staffs.map((st) => ({
                value: st.employeeId,
                label: `${st.fullName} (${st.roleName})`,
              }))}
            />
          </Form.Item>

          <Form.Item
            name="status"
            label="Trạng thái thực hiện"
            rules={[{ required: true, message: 'Vui lòng chọn trạng thái' }]}
          >
            <Select options={STATUS_OPTIONS} />
          </Form.Item>
        </div>

        <Form.Item name="notes" label="Ghi chú nhiệm vụ / Kết quả trực bể">
          <Input.TextArea
            rows={3}
            maxLength={500}
            showCount
            placeholder="Ghi chú nhiệm vụ kiểm tra chất lượng nước, vệ sinh lòng hồ, châm hóa chất..."
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}

