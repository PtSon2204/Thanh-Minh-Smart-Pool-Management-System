import { useId, useState } from 'react'
import { Alert, Button, Checkbox, DatePicker, Form, Select, Space, Typography } from 'antd'
import dayjs from 'dayjs'
import { contractDays, employmentTypes, formatHours, shiftLabel, weeklyHours } from '../contracts/contractUi'
import '../styles/staffContracts.css'

function WeeklyPatternInput({ value = [], onChange, shifts, disabled }) {
  const controlId = useId()
  const [morning, setMorning] = useState()
  const [afternoon, setAfternoon] = useState()
  const [presetDays, setPresetDays] = useState([1, 2, 3, 4, 5])
  const active = shifts.filter((shift) => shift.isActive !== false)
  const options = active.map((shift) => ({ value: shift.id, label: shiftLabel(shift) }))
  const updateDay = (dayOfWeek, shiftIds) => onChange(contractDays.map((_, index) => ({
    dayOfWeek: index + 1,
    shiftIds: index + 1 === dayOfWeek ? shiftIds : value.find((day) => day.dayOfWeek === index + 1)?.shiftIds || [],
  })))
  const applyPreset = (shiftIds) => onChange(contractDays.map((_, index) => ({
    dayOfWeek: index + 1,
    shiftIds: presetDays.includes(index + 1) ? shiftIds : value.find((day) => day.dayOfWeek === index + 1)?.shiftIds || [],
  })))
  return <div className="staff-contract-stack">
    <fieldset className="staff-contract-preset" disabled={disabled}>
      <legend>Mẫu lịch tuần</legend>
      <Typography.Paragraph type="secondary">Chọn ca có sẵn và ngày áp dụng. Mẫu thay các ngày đã chọn; các ngày khác giữ nguyên.</Typography.Paragraph>
      <div className="onboarding-form-grid">
        <label className="staff-contract-control" htmlFor={`${controlId}-morning`}>Ca cho mẫu sáng<Select id={`${controlId}-morning`} disabled={disabled} value={morning} onChange={setMorning} options={options} allowClear /></label>
        <label className="staff-contract-control" htmlFor={`${controlId}-afternoon`}>Ca cho mẫu chiều<Select id={`${controlId}-afternoon`} disabled={disabled} value={afternoon} onChange={setAfternoon} options={options} allowClear /></label>
      </div>
      <Checkbox.Group aria-label="Ngày áp dụng mẫu" disabled={disabled} value={presetDays} onChange={setPresetDays} options={contractDays.map((label, index) => ({ label, value: index + 1 }))} />
      <Space wrap>
        <Button disabled={disabled || !active.some((shift) => shift.id === morning) || !presetDays.length} onClick={() => applyPreset([morning])}>Áp dụng mẫu sáng</Button>
        <Button disabled={disabled || !active.some((shift) => shift.id === afternoon) || !presetDays.length} onClick={() => applyPreset([afternoon])}>Áp dụng mẫu chiều</Button>
        <Button disabled={disabled || !active.some((shift) => shift.id === morning) || !active.some((shift) => shift.id === afternoon) || morning === afternoon || !presetDays.length} onClick={() => applyPreset([morning, afternoon])}>Áp dụng cả ngày (hai ca)</Button>
      </Space>
    </fieldset>
    {contractDays.map((label, index) => {
      const selected = value.find((day) => day.dayOfWeek === index + 1)?.shiftIds || []
      const inactive = selected.filter((id) => !active.some((shift) => shift.id === id)).map((id) => {
        const shift = shifts.find((item) => item.id === id)
        return { value: id, label: shift ? `${shiftLabel(shift)} · Không còn hoạt động` : 'Ca chưa có thông tin', disabled: true }
      })
      return <label key={label} htmlFor={`${controlId}-${index}`} className="staff-contract-day"><span>{label}</span><Select id={`${controlId}-${index}`} mode="multiple" disabled={disabled} value={selected} onChange={(ids) => updateDay(index + 1, ids)} options={[...options, ...inactive]} optionFilterProp="label" allowClear placeholder="Nghỉ / chọn các ca làm việc" /></label>
    })}
    <Typography.Text strong role="status">Dự kiến: {formatHours(weeklyHours(value, shifts))}</Typography.Text>
  </div>
}

export default function ContractTermsFields({ form, shifts, disabled, invitation = false }) {
  const dateProps = { getValueProps: (value) => ({ value: value ? dayjs(value) : null }), getValueFromEvent: (value) => value?.format('YYYY-MM-DD') }
  return <section className="staff-contract-stack" aria-label="Điều khoản hợp đồng">
    <Typography.Title level={4}>Hợp đồng và lịch làm việc</Typography.Title>
    <Alert type="info" showIcon title="Ngày kết thúc được tính trong hợp đồng" description="Chỉ sinh lịch sau khi duyệt. Bỏ qua ngày hiện tại và quá khứ; ca trùng hoặc không hợp lệ được báo riêng." />
    <Form.Item name={['contract', 'employmentType']} label="Loại hợp đồng" rules={[{ required: true, message: 'Chọn loại hợp đồng.' }]}><Select disabled={disabled} options={employmentTypes} /></Form.Item>
    <div className="onboarding-form-grid">
      <Form.Item name={['contract', 'startDate']} label="Ngày bắt đầu hợp đồng" {...dateProps} rules={[{ required: true, message: 'Chọn ngày bắt đầu.' }]}><DatePicker disabled={disabled || invitation} format="DD/MM/YYYY" className="service-full-width" /></Form.Item>
      <Form.Item name={['contract', 'endDate']} label="Ngày kết thúc hợp đồng" {...dateProps} dependencies={[[ 'contract', 'startDate' ]]} rules={[{ required: true, message: 'Chọn ngày kết thúc.' }, { validator: (_, value) => !value || !form.getFieldValue(['contract', 'startDate']) || value >= form.getFieldValue(['contract', 'startDate']) ? Promise.resolve() : Promise.reject(new Error('Ngày kết thúc phải từ ngày bắt đầu trở đi.')) }]}><DatePicker disabled={disabled} format="DD/MM/YYYY" className="service-full-width" /></Form.Item>
    </div>
    <Form.Item name={['contract', 'weeklyPattern']} label="Ca làm theo ngày trong tuần" rules={[{ validator: (_, value) => value?.some((day) => day.shiftIds?.length) ? Promise.resolve() : Promise.reject(new Error('Chọn ít nhất một ca làm trong tuần.')) }]}><WeeklyPatternInput shifts={shifts} disabled={disabled} /></Form.Item>
  </section>
}
