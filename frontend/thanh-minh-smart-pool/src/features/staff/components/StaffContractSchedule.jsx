import { useId, useState } from 'react'
import { Alert, Button, DatePicker, Spin, Table, Tag, Typography } from 'antd'
import { useQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import 'dayjs/locale/vi'
import staffService from '../services/staffService'
import { workPatternService } from '../services/workPatternService'
import { shiftLabel } from '../contracts/contractUi'
import { formatOnboardingDate, onboardingError, vietnamToday } from '../onboarding/onboardingUi'
import { statusColor, statusLabel } from '../schedule/scheduleUtils'
import ContractTermsSummary from './ContractTermsSummary'
import '../styles/staffSchedule.css'

function DatedContractSchedule({ employeeId, contract, scope }) {
  const monthPickerId = useId()
  const [date, setDate] = useState(() => dayjs(vietnamToday() < contract.startDate ? contract.startDate : vietnamToday() > contract.endDate ? contract.endDate : vietnamToday()).locale('vi'))
  const [page, setPage] = useState(1)
  const monthStart = date.startOf('month').format('YYYY-MM-DD')
  const monthEnd = date.endOf('month').format('YYYY-MM-DD')
  const fromDate = monthStart < contract.startDate ? contract.startDate : monthStart
  const toDate = monthEnd > contract.endDate ? contract.endDate : monthEnd
  const query = useQuery({
    queryKey: ['staff-schedule-page', scope, employeeId, fromDate, toDate],
    queryFn: ({ signal }) => staffService.getCompleteSchedule(scope, { employeeId, fromDate, toDate }, signal),
    retry: false,
  })
  const assignments = [...(query.data?.items || [])].sort((first, second) => first.workDate.localeCompare(second.workDate) || first.startTime.localeCompare(second.startTime))
  const changeDate = (value) => {
    const next = value.format('YYYY-MM-DD') < contract.startDate ? dayjs(contract.startDate) : value.format('YYYY-MM-DD') > contract.endDate ? dayjs(contract.endDate) : value
    if (!next.isSame(date, 'month')) setPage(1)
    setDate(next.locale('vi'))
  }
  return <section className="staff-contract-stack" aria-label="Lịch phân công thực tế theo hợp đồng">
    <Typography.Title level={4}>Lịch phân công thực tế</Typography.Title>
    <Typography.Paragraph type="secondary">Lịch dưới đây chỉ hiển thị phân công đã lưu, không phải dự báo từ mẫu tuần. Ngày trống chưa có phân công; ca đã hủy vẫn hiển thị để đối chiếu.</Typography.Paragraph>
    <div className="staff-contract-month-controls">
      <label className="staff-contract-month-label" htmlFor={monthPickerId}>Tháng xem lịch</label>
      <div className="staff-contract-month-actions">
      <Button className="staff-contract-month-previous" disabled={date.isSame(dayjs(contract.startDate), 'month')} onClick={() => changeDate(date.subtract(1, 'month'))}>Tháng trước</Button>
      <DatePicker id={monthPickerId} className="staff-contract-month-picker" picker="month" allowClear={false} value={date} format="MM/YYYY" minDate={dayjs(contract.startDate)} maxDate={dayjs(contract.endDate)} onChange={changeDate} />
      <Button className="staff-contract-month-next" disabled={date.isSame(dayjs(contract.endDate), 'month')} onClick={() => changeDate(date.add(1, 'month'))}>Tháng sau</Button>
      <Button className="staff-contract-month-refresh" loading={query.isFetching} onClick={() => query.refetch()}>Tải lại phân công</Button>
      </div>
    </div>
    <Typography.Text type="secondary">Phạm vi: {formatOnboardingDate(fromDate)} - {formatOnboardingDate(toDate)} (bao gồm hai ngày).</Typography.Text>
    {query.isError ? <Alert type="error" showIcon title="Không thể tải phân công thực tế" description={onboardingError(query.error)} action={<Button size="small" onClick={() => query.refetch()}>Thử lại</Button>} />
      : query.isPending ? <Spin aria-label="Đang tải phân công trong tháng" /> : <>
        <Table rowKey="id" size="small" dataSource={assignments} scroll={{ x: 680 }} pagination={{ current: page, pageSize: 10, onChange: setPage, showSizeChanger: false }} locale={{ emptyText: 'Chưa có phân công thực tế trong tháng này.' }} columns={[
          { title: 'Ngày làm', dataIndex: 'workDate', render: (value) => formatOnboardingDate(value) },
          { title: 'Ca và thời gian', render: (_, assignment) => shiftLabel(assignment) },
          { title: 'Nguồn lịch', dataIndex: 'isWorkPatternGenerated', render: (value) => value ? 'Theo hợp đồng' : 'Điều chỉnh riêng' },
          { title: 'Trạng thái', dataIndex: 'status', render: (value) => <Tag color={statusColor(value)}>{statusLabel(value)}</Tag> },
        ]} />
      </>}
  </section>
}

export default function StaffContractSchedule({ contract, shifts = [], employeeId, scope = 'all' }) {
  return <div className="staff-contract-stack">
    <ContractTermsSummary contract={contract} shifts={shifts} />
    {contract && employeeId && <DatedContractSchedule key={`${employeeId}:${contract.startDate}:${contract.endDate}`} employeeId={employeeId} contract={contract} scope={scope} />}
  </div>
}

export function StaffContractOverview({ employeeId }) {
  const query = useQuery({ queryKey: ['staff-work-pattern', employeeId], queryFn: () => workPatternService.get(employeeId), enabled: Boolean(employeeId), retry: false })
  if (query.isError) return <Alert type="error" showIcon title="Không thể tải hợp đồng" description={onboardingError(query.error)} action={<Button size="small" onClick={() => query.refetch()}>Thử lại</Button>} />
  if (query.isPending) return <Spin aria-label="Đang tải hợp đồng" />
  return <StaffContractSchedule contract={query.data.contract} shifts={query.data.shifts || []} employeeId={employeeId} />
}
