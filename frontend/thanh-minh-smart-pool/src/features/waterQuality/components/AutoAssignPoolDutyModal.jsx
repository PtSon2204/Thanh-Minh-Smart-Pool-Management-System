import { useState } from 'react';
import {
  Modal,
  Form,
  DatePicker,
  Select,
  Switch,
  Input,
  Alert,
  Space,
  Button,
  Typography,
  message,
} from 'antd';
import { ThunderboltOutlined, CalendarOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { waterQualityService } from '../services/waterQualityService';

const { RangePicker } = DatePicker;
const { Text } = Typography;

export default function AutoAssignPoolDutyModal({
  visible,
  options,
  defaultDateRange,
  onCancel,
  onSuccess,
}) {
  const [form] = Form.useForm();
  const [submitting, setSubmitting] = useState(false);

  const shifts = options?.shifts || [];
  const staffs = options?.staffs || [];

  const handleQuickRange = (type) => {
    const today = dayjs();
    if (type === 'thisWeek') {
      const start = today.startOf('week').add(1, 'day');
      const end = start.add(6, 'day');
      form.setFieldsValue({ dateRange: [start, end] });
    } else if (type === 'nextWeek') {
      const start = today.startOf('week').add(8, 'day');
      const end = start.add(6, 'day');
      form.setFieldsValue({ dateRange: [start, end] });
    } else if (type === 'next14Days') {
      form.setFieldsValue({ dateRange: [today, today.add(13, 'day')] });
    }
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setSubmitting(true);

      const [from, to] = values.dateRange;
      const payload = {
        fromDate: from.format('YYYY-MM-DD'),
        toDate: to.format('YYYY-MM-DD'),
        shiftIds: values.shiftIds?.length ? values.shiftIds : null,
        candidateEmployeeIds: values.candidateEmployeeIds?.length
          ? values.candidateEmployeeIds
          : null,
        prioritizeShiftRoster: values.prioritizeShiftRoster,
        overwriteExisting: values.overwriteExisting,
        defaultNotes: values.defaultNotes?.trim() || null,
      };

      const res = await waterQualityService.autoAssignDutySchedules(payload);
      message.success(
        res.message ||
          `Đã tự động phân công ${res.createdCount + res.updatedCount} ca trực bể!`
      );
      onSuccess(res);
      onCancel();
    } catch (error) {
      if (error?.response?.data?.message) {
        message.error(error.response.data.message);
      } else if (!error?.errorFields) {
        message.error('Không thể tự động phân công lịch trực bể.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      title={
        <Space>
          <ThunderboltOutlined style={{ color: '#faad14', fontSize: 20 }} />
          <span>Tự động gán lịch trực bể & Kiểm tra nước</span>
        </Space>
      }
      open={visible}
      onOk={handleSubmit}
      onCancel={onCancel}
      confirmLoading={submitting}
      okText="Tự động phân công"
      cancelText="Hủy"
      width={620}
      destroyOnClose
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Thuật toán phân công tự động cân bằng tải (Round-Robin)"
        description="Hệ thống sẽ ưu tiên nhân viên có ca làm việc tương ứng trong ngày và phân bổ đều số ca trực bể cho đội ngũ Cứu hộ / Kỹ thuật hồ bơi."
      />

      <Form
        form={form}
        layout="vertical"
        initialValues={{
          dateRange: defaultDateRange || [
            dayjs().startOf('week').add(1, 'day'),
            dayjs().startOf('week').add(7, 'day'),
          ],
          shiftIds: shifts.map((s) => s.shiftId),
          prioritizeShiftRoster: true,
          overwriteExisting: false,
          defaultNotes: 'Đo pH, Clo tự do đầu ca và kiểm tra vệ sinh mặt nước bể bơi',
        }}
      >
        <Form.Item
          name="dateRange"
          label={
            <Space>
              <span>Khoảng ngày phân công</span>
              <Button size="small" type="link" onClick={() => handleQuickRange('thisWeek')}>
                Tuần này
              </Button>
              <Button size="small" type="link" onClick={() => handleQuickRange('nextWeek')}>
                Tuần tới
              </Button>
              <Button size="small" type="link" onClick={() => handleQuickRange('next14Days')}>
                14 ngày tới
              </Button>
            </Space>
          }
          rules={[{ required: true, message: 'Vui lòng chọn khoảng ngày phân công' }]}
        >
          <RangePicker
            format="DD/MM/YYYY"
            style={{ width: '100%' }}
            suffixIcon={<CalendarOutlined />}
          />
        </Form.Item>

        <Form.Item
          name="shiftIds"
          label="Ca trực áp dụng (Để trống sẽ áp dụng tất cả các ca)"
        >
          <Select
            mode="multiple"
            allowClear
            placeholder="Tất cả ca trực đang hoạt động (Ca sáng, Ca chiều)"
            options={shifts.map((s) => ({
              value: s.shiftId,
              label: `${s.name} (${String(s.startTime).slice(0, 5)} - ${String(s.endTime).slice(0, 5)})`,
            }))}
          />
        </Form.Item>

        <Form.Item
          name="candidateEmployeeIds"
          label="Nhóm nhân sự tham gia xoay vòng (Để trống sẽ tự động chọn đội Cứu hộ / Kỹ thuật)"
        >
          <Select
            mode="multiple"
            allowClear
            placeholder="Tự động chọn tất cả nhân viên Cứu hộ / Kỹ thuật đủ điều kiện"
            options={staffs.map((st) => ({
              value: st.employeeId,
              label: `${st.fullName} (${st.roleName})`,
            }))}
          />
        </Form.Item>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
          <Form.Item
            name="prioritizeShiftRoster"
            label="Ưu tiên theo lịch ca làm việc"
            valuePropName="checked"
          >
            <Switch
              checkedChildren="Ưu tiên người có ca"
              unCheckedChildren="Xoay vòng toàn bộ"
            />
          </Form.Item>

          <Form.Item
            name="overwriteExisting"
            label="Xử lý các ca đã có người trực"
            valuePropName="checked"
          >
            <Switch
              checkedChildren="Ghi đè lịch cũ"
              unCheckedChildren="Giữ nguyên lịch cũ"
            />
          </Form.Item>
        </div>

        <Form.Item name="defaultNotes" label="Ghi chú nhiệm vụ trực bể mặc định">
          <Input.TextArea
            rows={2}
            maxLength={500}
            showCount
            placeholder="VD: Đo pH, Clo tự do đầu ca, kiểm tra độ trong và vệ sinh máng tràn..."
          />
        </Form.Item>

        <Text type="secondary" style={{ fontSize: 12 }}>
          * Các nhân viên được phân công sẽ tự động nhận được thông báo trên hệ thống.
        </Text>
      </Form>
    </Modal>
  );
}

