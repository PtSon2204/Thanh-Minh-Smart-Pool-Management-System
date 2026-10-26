import { ClockCircleOutlined, SaveOutlined, TeamOutlined } from '@ant-design/icons'
import { Alert, Button, Card, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import { vietnamPickerTimeToUtc } from '../utils/classSchedule'

const DATE_FORMAT = 'DD/MM/YYYY HH:mm'
const TIME_PICKER = { format: 'HH:mm', minuteStep: 15 }

export default function SwimClassInfoForm({ form, onFinish, pending, coachesQuery, coachOptions, error }) {
  return <Card className="swim-class-form-card" title={<><TeamOutlined /> Thông tin lớp học</>}>
    <p className="swim-class-card-intro">Lớp được lưu ở trạng thái bản nháp. Bạn sẽ thêm lịch học trước khi kích hoạt.</p>
    {error && <Alert className="swim-class-form-alert" type="error" showIcon title="Chưa tạo được lớp" description={error} />}
    <Form form={form} layout="vertical" initialValues={{ capacity: 10, fee: 0 }} onFinish={onFinish} disabled={pending}>
      <div className="swim-class-form-grid">
        <Form.Item className="swim-class-span-full" label="Tên lớp" name="name" rules={[
          { required: true, whitespace: true, message: 'Nhập tên lớp.' },
          { transform: (value) => value?.trim(), max: 150, message: 'Tên lớp tối đa 150 ký tự.' },
        ]}><Input maxLength={150} showCount placeholder="Ví dụ: Lớp bơi căn bản buổi sáng" /></Form.Item>
        <Form.Item className="swim-class-span-full" label="Mô tả" name="description"><Input.TextArea rows={3} placeholder="Thông tin dành cho học viên và phụ huynh" /></Form.Item>
        <Form.Item label="Sức chứa" name="capacity" rules={[{ required: true, message: 'Nhập sức chứa.' }, { type: 'number', min: 1, message: 'Sức chứa phải lớn hơn 0.' }]}><InputNumber min={1} precision={0} className="swim-class-full-width" addonAfter="học viên" /></Form.Item>
        <Form.Item label="Học phí" name="fee" rules={[{ required: true, message: 'Nhập học phí.' }, { type: 'number', min: 0, message: 'Học phí không được âm.' }]}><InputNumber min={0} precision={2} className="swim-class-full-width" addonAfter="VNĐ" /></Form.Item>
        <Form.Item className="swim-class-span-full" label="Coach mặc định" name="coachUserId" rules={[{ required: true, message: 'Chọn Coach phụ trách lớp.' }]} extra="Coach này được dùng cho các buổi không chọn người dạy riêng.">
          <Select showSearch optionFilterProp="label" loading={coachesQuery.isLoading} disabled={coachesQuery.isError} placeholder="Chọn Coach đang làm việc" options={coachOptions} notFoundContent="Chưa có Coach phù hợp" />
        </Form.Item>
        <Form.Item label="Bắt đầu nhận đăng ký" name="registrationStartAt" rules={[{ required: true, message: 'Chọn thời gian bắt đầu.' }]}><DatePicker className="swim-class-full-width" showTime={TIME_PICKER} format={DATE_FORMAT} placeholder="Ngày và giờ" /></Form.Item>
        <Form.Item label="Kết thúc nhận đăng ký" name="registrationEndAt" dependencies={['registrationStartAt']} rules={[
          { required: true, message: 'Chọn thời gian kết thúc.' },
          ({ getFieldValue }) => ({ validator(_, value) {
            const start = getFieldValue('registrationStartAt')
            return !value || !start || vietnamPickerTimeToUtc(value) > vietnamPickerTimeToUtc(start)
              ? Promise.resolve() : Promise.reject(new Error('Thời gian kết thúc phải sau thời gian bắt đầu.'))
          } }),
        ]}><DatePicker className="swim-class-full-width" showTime={TIME_PICKER} format={DATE_FORMAT} placeholder="Ngày và giờ" /></Form.Item>
      </div>
      <div className="swim-class-time-note"><ClockCircleOutlined /> Thời gian hiển thị theo giờ Việt Nam (UTC+7).</div>
      <div className="swim-class-actions"><Button type="primary" htmlType="submit" size="large" icon={<SaveOutlined />} loading={pending} disabled={coachesQuery.isError || !coachOptions.length}>Lưu lớp và tiếp tục</Button></div>
    </Form>
  </Card>
}
