import { Alert, Descriptions, Table, Typography } from 'antd'
import { formatOnboardingDate } from '../onboarding/onboardingUi'
import '../styles/staffContracts.css'

export default function ScheduleOperationResult({ result, preview = false }) {
  if (!result) return null
  const hasConflicts = result.conflictCount > 0
  const tasks = result.affectedTasks || []
  const details = result.details || []
  return <section className="staff-contract-stack" aria-label={preview ? 'Kết quả xem trước lịch làm việc' : 'Kết quả cập nhật lịch làm việc'} aria-live="polite">
    <Alert type={hasConflicts || tasks.length ? 'warning' : 'info'} showIcon title={preview ? 'Xem trước thay đổi, chưa lưu' : hasConflicts ? 'Đã xử lý nhưng còn xung đột' : 'Kết quả xử lý từ máy chủ'} description={<>Trạng thái: {result.status}. {hasConflicts ? 'Không phải toàn bộ lịch đã được tạo. Kiểm tra từng ngày bên dưới.' : 'Kiểm tra số ca và ngày bị bỏ qua bên dưới.'}</>} />
    <Descriptions bordered column={{ xs: 1, sm: 2 }} size="small">
      <Descriptions.Item label={preview ? 'Sẽ tạo' : 'Đã tạo'}>{result.createdCount}</Descriptions.Item>
      <Descriptions.Item label={preview ? 'Sẽ hủy' : 'Đã hủy'}>{result.cancelledCount}</Descriptions.Item>
      <Descriptions.Item label="Bỏ qua">{result.skippedCount}</Descriptions.Item>
      <Descriptions.Item label="Xung đột">{result.conflictCount}</Descriptions.Item>
    </Descriptions>
    {details.length > 0 && <Table size="small" rowKey={(item) => `${item.workDate}-${item.shiftId || 'none'}-${item.code}`} dataSource={details} pagination={{ pageSize: 10 }} scroll={{ x: 520 }} columns={[
      { title: 'Ngày', dataIndex: 'workDate', render: (value) => formatOnboardingDate(value) },
      { title: 'Mã ca', dataIndex: 'shiftId' },
      { title: 'Mã kết quả', dataIndex: 'code' },
      { title: 'Chi tiết', dataIndex: 'message' },
    ]} />}
     {tasks.length > 0 && <><Typography.Title level={5}>Công việc vận hành bị ảnh hưởng</Typography.Title><Alert type="warning" showIcon title="Kiểm tra và phân công lại công việc" description="Công việc không còn người trực hợp lệ có thể trở thành chưa phân công. Không coi thao tác lịch là đã phân công lại công việc." /><Table size="small" rowKey="id" dataSource={tasks} pagination={{ pageSize: 10 }} scroll={{ x: 480 }} columns={[
      { title: 'Công việc', render: (_, task) => task.name || task.title || task.taskId || task.id },
       { title: 'Ngày', dataIndex: 'workDate', render: (value) => formatOnboardingDate(value) },
      { title: 'Chi tiết', render: (_, task) => task.message || task.status || task.code || JSON.stringify(task) },
    ]} /></>}
  </section>
}
