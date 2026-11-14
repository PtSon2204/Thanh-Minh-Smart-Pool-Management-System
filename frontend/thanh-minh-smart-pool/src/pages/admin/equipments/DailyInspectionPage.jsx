import { useQuery } from '@tanstack/react-query'
import { Alert, Button, ConfigProvider, Spin } from 'antd'
import dayjs from 'dayjs'
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { SafetyCertificateOutlined } from '@ant-design/icons'
import DailyInspectionForm from '../../../features/equipments/components/DailyInspectionForm'
import { useAuthStore } from '../../../features/auth/store/authStore'
import operationalTaskService from '../../../features/staff/services/operationalTaskService'
import { FIXED_INSPECTION_SHIFT_IDS } from '../../../features/equipments/inspectionConstants'
import '../../../features/equipments/styles/equipmentManagement.css'

export default function DailyInspectionPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const session = useAuthStore((state) => state.session)
  const isAdmin = session?.role?.toUpperCase() === 'ADMIN'
  const operationalTaskId = searchParams.get('taskId')
  const initialShiftId = searchParams.get('shiftId')
  const passedTask = location.state?.task?.id === operationalTaskId ? location.state.task : null
  const taskLookup = useQuery({
    queryKey: ['operational-task-execution-context', operationalTaskId],
    queryFn: ({ signal }) => operationalTaskService.getTasks({ taskId: operationalTaskId }, signal),
    enabled: Boolean(operationalTaskId) && !passedTask,
    staleTime: 30_000,
  })
  const task = passedTask || taskLookup.data?.items?.find((item) => item.id === operationalTaskId)
  const adminTaskExecution = isAdmin && task ? {
    id: task.id,
    taskType: task.taskType,
    revision: task.revision,
    assignedEmployeeId: task.assignedEmployeeId ?? null,
    assignedEmployeeName: task.assignedEmployeeName || 'Chưa phân công',
    dutyDate: task.dutyDate,
    performerName: session?.fullName || session?.userName || 'Quản trị viên',
  } : null

  const fallbackWeek = task?.dutyDate
    ? dayjs(task.dutyDate).subtract((dayjs(task.dutyDate).day() + 6) % 7, 'day').format('YYYY-MM-DD')
    : null
  const fallbackReturnTo = fallbackWeek
    ? `/admin/staff/tasks?week=${fallbackWeek}&view=list`
    : '/admin/staff/tasks'
  const candidate = location.state?.returnTo || searchParams.get('returnTo')
  const returnTo = typeof candidate === 'string' ? candidate : fallbackReturnTo
  const isSafeReturnTo = returnTo === '/admin/staff/tasks' || returnTo.startsWith('/admin/staff/tasks?')
  const returnToTasks = () => navigate(isSafeReturnTo ? returnTo : fallbackReturnTo, { replace: true })
  const handleSuccess = () => {
    if (operationalTaskId) {
      returnToTasks()
    } else if (isAdmin) {
      navigate('/admin/equipments/inspection-history')
    }
  }

  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#0068ff',
          colorInfo: '#0068ff',
          borderRadius: 8,
        },
      }}
    >
      <div className="equipment-list-page" style={{ padding: '24px' }}>
        <header className="equipment-page-header" style={{ marginBottom: 20 }}>
          <div className="equipment-page-heading">
            <span className="equipment-header-icon" aria-hidden="true">
              <SafetyCertificateOutlined />
            </span>
            <div>
              <h1 style={{ fontSize: 22, fontWeight: 700, margin: 0, color: '#001f3e' }}>
                Kiểm tra thiết bị đầu ca 
              </h1>
              <p style={{ margin: '4px 0 0 0', color: '#6b7280', fontSize: 14 }}>
                Quy trình kiểm tra vận hành & an toàn trước ca làm việc. Thiết bị phát hiện sự cố sẽ được ghi nhận để thông báo xử lý.
              </p>
            </div>
          </div>
        </header>

        {operationalTaskId && <Button onClick={returnToTasks}>Quay lại công việc vận hành</Button>}
        {operationalTaskId && !task ? (
          taskLookup.isError ? (
            <Alert type="error" showIcon title="Không thể tải công việc" description="Hãy quay lại danh sách công việc và mở lại checklist." />
          ) : taskLookup.isPending ? (
            <Spin tip="Đang tải thông tin công việc..." />
          ) : (
            <Alert type="warning" showIcon title="Không tìm thấy công việc được yêu cầu." />
          )
        ) : operationalTaskId && (task.taskType !== 'EquipmentChecklist' || task.status === 'Completed' || task.status === 'Cancelled') ? (
          <Alert type="warning" showIcon title="Công việc này không thể nộp checklist." />
        ) : (
          <DailyInspectionForm
            key={operationalTaskId || 'standalone'}
            onSuccess={handleSuccess}
            operationalTaskId={operationalTaskId}
            initialShiftId={task ? task.inspectionWindow === 'MORNING' ? FIXED_INSPECTION_SHIFT_IDS.morning : FIXED_INSPECTION_SHIFT_IDS.afternoon : initialShiftId}
            operationalTaskDate={task?.dutyDate}
            adminTaskExecution={adminTaskExecution}
          />
        )}
      </div>
    </ConfigProvider>
  )
}

