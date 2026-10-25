import { CalendarOutlined, ClockCircleOutlined, DeleteOutlined, PlusOutlined, SaveOutlined } from '@ant-design/icons'
import { Alert, Button, Card, DatePicker, Form, Select } from 'antd'
import { coachName, conflictDescription } from '../utils/classDisplay'
import { vietnamPickerTimeToUtc } from '../utils/classSchedule'

const DATE_FORMAT = 'DD/MM/YYYY HH:mm'
const TIME_PICKER = { format: 'HH:mm', minuteStep: 15 }

export default function ClassSessionsEditor({ form, onFinish, pending, coachesQuery, coachOptions, coaches, draft, error, conflicts, onValuesChange }) {
  return <Card className="swim-class-form-card" title={<><CalendarOutlined /> Thêm buổi học</>}>
    <p className="swim-class-card-intro">Có thể nhập nhiều buổi cùng lúc. Nếu không chọn Coach riêng, buổi học dùng Coach mặc định của lớp.</p>
    {error && <Alert className="swim-class-form-alert" type="error" showIcon title="Chưa lưu được lịch học" description={error} />}
    {conflicts.length > 0 && <Alert className="swim-class-form-alert" type="warning" showIcon title="Các buổi bị trùng lịch" description={<ul className="swim-class-conflict-list">{conflicts.map((conflict, index) => <li key={`${conflict.sessionIndex}-${index}`}>{conflictDescription(conflict, coaches)}</li>)}</ul>} />}
    <Form form={form} layout="vertical" initialValues={{ sessions: [{}] }} onFinish={onFinish} disabled={pending} onValuesChange={onValuesChange}>
      <Form.List name="sessions" rules={[{ validator: (_, value) => value?.length ? Promise.resolve() : Promise.reject(new Error('Cần ít nhất một buổi học.')) }]}>
        {(fields, { add, remove }, { errors }) => <>
          {fields.map((field, index) => <div className="swim-class-session-card" key={field.key}>
            <div className="swim-class-session-heading"><span>Buổi {index + 1}</span><Button type="text" danger icon={<DeleteOutlined />} disabled={fields.length === 1} onClick={() => remove(field.name)} aria-label={`Xóa buổi ${index + 1}`}>Xóa</Button></div>
            <div className="swim-class-form-grid">
              <Form.Item label="Bắt đầu" name={[field.name, 'startsAt']} rules={[
                { required: true, message: 'Chọn giờ bắt đầu.' },
                { validator: (_, value) => !value || Date.parse(vietnamPickerTimeToUtc(value)) > Date.now() ? Promise.resolve() : Promise.reject(new Error('Buổi học phải bắt đầu trong tương lai.')) },
              ]}><DatePicker className="swim-class-full-width" showTime={TIME_PICKER} format={DATE_FORMAT} placeholder="Ngày và giờ" /></Form.Item>
              <Form.Item label="Kết thúc" name={[field.name, 'endsAt']} dependencies={[['sessions', field.name, 'startsAt']]} rules={[
                { required: true, message: 'Chọn giờ kết thúc.' },
                { validator: (_, value) => {
                  const start = form.getFieldValue(['sessions', field.name, 'startsAt'])
                  return !value || !start || vietnamPickerTimeToUtc(value) > vietnamPickerTimeToUtc(start)
                    ? Promise.resolve() : Promise.reject(new Error('Giờ kết thúc phải sau giờ bắt đầu.'))
                } },
              ]}><DatePicker className="swim-class-full-width" showTime={TIME_PICKER} format={DATE_FORMAT} placeholder="Ngày và giờ" /></Form.Item>
              <Form.Item className="swim-class-span-full" label="Coach phụ trách" name={[field.name, 'coachUserId']} extra={`Để trống để dùng Coach mặc định: ${coachName(coaches, draft.coachUserId)}.`}>
                <Select allowClear showSearch optionFilterProp="label" loading={coachesQuery.isLoading} placeholder="Dùng Coach mặc định" options={coachOptions} />
              </Form.Item>
            </div>
          </div>)}
          <Form.ErrorList errors={errors} />
          <Button className="swim-class-add-session" type="dashed" icon={<PlusOutlined />} onClick={() => add({})}>Thêm buổi học</Button>
        </>}
      </Form.List>
      <div className="swim-class-time-note"><ClockCircleOutlined /> Giờ nhập theo Việt Nam (UTC+7). Các buổi trùng nhau không thể lưu.</div>
      <div className="swim-class-actions"><Button type="primary" htmlType="submit" size="large" icon={<SaveOutlined />} loading={pending}>Lưu các buổi học</Button></div>
    </Form>
  </Card>
}
