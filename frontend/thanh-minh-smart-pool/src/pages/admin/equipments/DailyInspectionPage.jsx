import { useEffect } from 'react'
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
  const isLifeguard = session?.role?.toUpperCase() === 'LIFEGUARD'

  // Trích xuất context từ location.state hoặc query params (nếu người dùng bấm link cũ)
  const stateContext = location.state || {}
  const rawTaskId = stateContext.taskId || stateContext.task?.id || searchParams.get('taskId')
  const initialShiftId = stateContext.shiftId || searchParams.get('shiftId')
  const passedTask = stateContext.task || null

  // URL Normalization: Tự động xóa sạch Query Params khỏi thanh địa chỉ nếu có,
  // giữ nguyên toàn bộ state để thanh URL luôn sạch đẹp: /admin/equipments/daily-inspections
  useEffect(() => {
    const qTaskId = searchParams.get('taskId')
    const qShiftId = searchParams.get('shiftId')
    const qReturnTo = searchParams.get('returnTo')
    if (qTaskId || qShiftId || qReturnTo) {
      navigate('/admin/equipments/daily-inspections', {
        replace: true,
        state: {
          ...location.state,
          taskId: qTaskId || location.state?.taskId,
          shiftId: qShiftId || location.state?.shiftId,
          returnTo: qReturnTo || location.state?.returnTo,
          task: passedTask || location.state?.task,
        },
      })
    }
  }, [searchParams, location.state, navigate, passedTask])

  // 1. Direct task lookup when taskId is explicitly provided and task object wasn't passed
  const taskLookup = useQuery({
    queryKey: ['operational-task-execution-context', rawTaskId],
    queryFn: ({ signal }) => operationalTaskService.getTasks({ taskId: rawTaskId }, signal),
    enabled: Boolean(rawTaskId) && !passedTask,
    staleTime: 30_000,
  })

  // 2. Auto-resolve task for lifeguard accessing from sidebar without taskId
  const todayVietnam = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
  const autoTaskLookup = useQuery({
    queryKey: ['lifeguard-today-checklist-task', session?.id, todayVietnam],
    queryFn: ({ signal }) => operationalTaskService.getTasks({
      fromDate: todayVietnam,
      toDate: todayVietnam,
      taskType: 'EquipmentChecklist',
    }, signal),
    enabled: isLifeguard && !rawTaskId && Boolean(session?.id),
    staleTime: 30_000,
  })

  const todayTasks = autoTaskLookup.data?.items?.filter(
    (item) => item.status !== 'Completed' && item.status !== 'Cancelled'
  ) || []
  const preferredWindow = dayjs().hour() < 12 ? 'MORNING' : 'AFTERNOON'
  const autoTask = todayTasks.find((t) => t.inspectionWindow === preferredWindow) || todayTasks[0] || null

  const task = passedTask
    || (rawTaskId ? taskLookup.data?.items?.find((item) => item.id === rawTaskId) : null)
    || autoTask

  const operationalTaskId = rawTaskId || autoTask?.id
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
  
  const hasTaskContext = Boolean(rawTaskId || location.state?.returnTo || location.state?.task)
  const handleSuccess = () => {
    if (hasTaskContext) {
      returnToTasks()
    } else if (isAdmin) {
      navigate('/admin/equipments/inspection-history')
    } else {
      navigate('/admin/staff/tasks')
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
                Quy trình kiểm tra vận hành & an toàn trước ca làm việc.
              </p>
            </div>
          </div>
        </header>

        {hasTaskContext && (
          <Button onClick={returnToTasks} style={{ marginBottom: 16 }}>
            Quay lại công việc vận hành
          </Button>
        )}

        {isLifeguard && !rawTaskId && autoTaskLookup.isPending ? (
          <Spin tip="Đang kiểm tra nhiệm vụ kiểm tra thiết bị hôm nay..." />
        ) : isLifeguard && !rawTaskId && !autoTask ? (
          <div style={{ maxWidth: 640, marginTop: 12 }}>
            <Alert
              type="warning"
              showIcon
              title="Chưa có ca trực hoặc nhiệm vụ kiểm tra thiết bị hôm nay"
              description="Bạn chưa được phân công ca trực hoặc nhiệm vụ kiểm tra thiết bị cho ngày hôm nay. Vui lòng liên hệ Quản trị viên để được phân ca, hoặc truy cập trang Công việc vận hành."
              action={
                <Button type="primary" onClick={() => navigate('/admin/staff/tasks')}>
                  Xem công việc vận hành
                </Button>
              }
            />
          </div>
        ) : operationalTaskId && !task ? (
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

