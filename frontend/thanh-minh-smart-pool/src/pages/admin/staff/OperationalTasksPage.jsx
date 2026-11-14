import { useCallback, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  Alert, Button, DatePicker, Descriptions, Empty, Form, Grid, message, Modal,
  Segmented, Select, Space, Spin, Table, Tag, TimePicker, Typography,
} from 'antd'
import {
  CalendarOutlined, EyeOutlined, LeftOutlined, ReloadOutlined,
  RightOutlined, ScheduleOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import 'dayjs/locale/vi'
import viDatePicker from 'antd/locale/vi_VN'
import { useAuthStore } from '../../../features/auth/store/authStore'
import { useScheduleEmployees } from '../../../features/staff/hooks/useStaffs'
import { useActiveShifts } from '../../../features/staff/hooks/useShifts'
import operationalTaskService from '../../../features/staff/services/operationalTaskService'
import { FIXED_INSPECTION_SHIFT_IDS } from '../../../features/equipments/inspectionConstants'
import CreateWaterQualityLogModal from '../../../features/waterQuality/components/CreateWaterQualityLogModal'
import { ServiceListHeader } from '../../../features/services/components/ServiceListChrome'
import '../../../features/services/styles/serviceManagement.css'
import '../../../features/staff/styles/staffSchedule.css'
import '../../../features/staff/styles/operationalTasks.css'
import '../../../features/staff/styles/operationalTasks.css'

const todayVietnam = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
const displayDate = (value) => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '—'
const displayTime = (value) => value ? new Intl.DateTimeFormat('vi-VN', { timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : 'Chưa xác định'
const weekMonday = (value) => {
  const date = dayjs(value)
  return date.subtract((date.day() + 6) % 7, 'day').startOf('day')
}
const vietnamDate = (value) => new Intl.DateTimeFormat('vi-VN', {
  weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'Asia/Ho_Chi_Minh',
}).format(new Date(`${value}T12:00:00+07:00`))
const taskTypeLabel = (type) => ({ Measurement: 'Đo chất lượng nước', EquipmentChecklist: 'Kiểm tra thiết bị', Cleaning: 'Xử lý chất lượng nước' }[type] || type || 'Công việc')
const taskStatus = (status) => ({ Scheduled: ['Đã lên lịch', 'processing'], Unassigned: ['Chưa phân công', 'warning'], InProgress: ['Đang xử lý', 'processing'], Completed: ['Hoàn tất', 'success'], Cancelled: ['Đã hủy', 'default'], Absent: ['Vắng mặt', 'default'] }[status] || [status, 'default'])
const terminal = (status) => status === 'Completed' || status === 'Cancelled'

export default function OperationalTasksPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const taskId = searchParams.get('taskId')
  const session = useAuthStore((state) => state.session)
  const isAdmin = session?.role?.toUpperCase() === 'ADMIN'
  const isLifeguard = session?.role?.toUpperCase() === 'LIFEGUARD'
  const taskLookup = useQuery({
    queryKey: ['operational-task-deep-link', taskId],
    queryFn: ({ signal }) => operationalTaskService.getTasks({ taskId }, signal),
    enabled: Boolean(taskId) && (isAdmin || isLifeguard),
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
  })
  const linkedTask = taskId ? taskLookup.data?.items?.find((task) => task.id === taskId) : null
  const weekStart = useMemo(() => {
    if (linkedTask) return weekMonday(linkedTask.dutyDate)
    const requestedWeek = searchParams.get('week')
    const parsedWeek = requestedWeek && /^\d{4}-\d{2}-\d{2}$/.test(requestedWeek) && dayjs(requestedWeek).isValid()
      ? requestedWeek
      : todayVietnam()
    return weekMonday(parsedWeek)
  }, [searchParams, linkedTask])
  const weekStartKey = weekStart.format('YYYY-MM-DD')
  const weekEndKey = weekStart.add(6, 'day').format('YYYY-MM-DD')
  const view = searchParams.get('view') === 'table' ? 'table' : 'list'
  const taskTypeFilter = searchParams.get('taskType') || undefined
  const statusFilter = searchParams.get('status') || undefined
  const employeeFilter = isAdmin ? searchParams.get('employeeId') || undefined : undefined
  const [taskForAssignment, setTaskForAssignment] = useState(null)
  const [selectedEmployeeShiftId, setSelectedEmployeeShiftId] = useState()
  const [selectedMeasurementTask, setMeasurementTask] = useState(null)
  const measurementTask = selectedMeasurementTask || (linkedTask && !terminal(linkedTask.status)
    && linkedTask.taskType !== 'EquipmentChecklist'
    && (isAdmin || linkedTask.assignedEmployeeId === session?.id) ? linkedTask : null)
  const [selectedChecklistResultTask, setChecklistResultTask] = useState(null)
  const checklistResultTask = selectedChecklistResultTask || (linkedTask?.status === 'Completed' && linkedTask.dailyInspectionId ? linkedTask : null)
  const [showOverdue, setShowOverdue] = useState(false)
  const [recurrenceOpen, setRecurrenceOpen] = useState(false)
  const [recurrenceForm] = Form.useForm()
  const handledDeepLink = useRef(null)
  const screens = Grid.useBreakpoint()
  const employees = useScheduleEmployees(isAdmin)
  const shifts = useActiveShifts(isAdmin)
  const roster = employees.data || []
  const lifeguards = roster.filter((employee) => employee.roleName?.toUpperCase() === 'LIFEGUARD')
  const shiftOptions = shifts.data?.items || []
  const taskParams = useMemo(() => ({
    pageIndex: 1,
    pageSize: 100,
    ...(taskTypeFilter ? { taskType: taskTypeFilter } : {}),
    ...(statusFilter ? { status: statusFilter } : {}),
    ...(employeeFilter ? { employeeId: employeeFilter } : {}),
    fromDate: weekStartKey, toDate: weekEndKey,
  }), [taskTypeFilter, statusFilter, employeeFilter, weekStartKey, weekEndKey])

  const tasks = useQuery({
    queryKey: ['operational-tasks', taskParams],
    queryFn: ({ signal }) => operationalTaskService.getTasks(taskParams, signal),
    enabled: (isAdmin || isLifeguard) && (!taskId || Boolean(linkedTask)),
  })
  const overdueFilters = useMemo(() => ({
    ...(taskTypeFilter ? { taskType: taskTypeFilter } : {}),
    ...(employeeFilter ? { employeeId: employeeFilter } : {}),
  }), [taskTypeFilter, employeeFilter])
  const overdueTasks = useQuery({
    queryKey: ['operational-task-overdue', weekStartKey, overdueFilters],
    queryFn: ({ signal }) => operationalTaskService.getOverdueTasks(
      weekStart.subtract(1, 'day').format('YYYY-MM-DD'), overdueFilters, signal
    ),
    enabled: isAdmin || isLifeguard,
  })
  const recurrences = useQuery({
    queryKey: ['operational-task-recurrences'],
    queryFn: operationalTaskService.getRecurrences,
    enabled: isAdmin,
  })
  const assignees = useQuery({
    queryKey: ['operational-task-assignees', taskForAssignment?.id],
    queryFn: () => operationalTaskService.getAssignees(taskForAssignment.id),
    enabled: Boolean(taskForAssignment),
  })
  const checklistResult = useQuery({
    queryKey: ['operational-task-checklist-result', checklistResultTask?.id],
    queryFn: ({ signal }) => operationalTaskService.getChecklistResult(checklistResultTask.id, signal),
    enabled: Boolean(checklistResultTask?.id),
    retry: false,
  })

  const refreshTasks = useCallback(() => Promise.all([
    queryClient.invalidateQueries({ queryKey: ['operational-tasks'] }),
    queryClient.invalidateQueries({ queryKey: ['operational-task-overdue'] }),
  ]), [queryClient])
  const ensureWeek = useMutation({
    mutationFn: operationalTaskService.ensureWeek,
    onSuccess: (result) => {
      if (result.createdCount > 0) message.success(`Đã chuẩn bị ${result.createdCount} công việc cho tuần.`)
      refreshTasks()
    },
    onError: (error) => message.error(error.response?.data?.message || 'Không thể chuẩn bị công việc cho tuần.'),
  })
  const updateSearch = useCallback((update, replace = true) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      update(next)
      return next
    }, { replace })
  }, [setSearchParams])
  const selectWeek = (nextWeek, replace = false) => updateSearch((params) => {
    params.delete('taskId')
    params.set('week', weekMonday(nextWeek).format('YYYY-MM-DD'))
    if (view === 'list') params.delete('view')
    else params.set('view', view)
  }, replace)
  const selectView = (nextView) => updateSearch((params) => {
    params.delete('taskId')
    if (nextView === 'list') params.delete('view')
    else params.set('view', nextView)
  })
  const updateFilter = (key, value) => updateSearch((params) => {
    params.delete('taskId')
    if (value) params.set(key, value)
    else params.delete(key)
  })
  const assign = useMutation({
    mutationFn: ({ id, payload }) => operationalTaskService.assign(id, payload),
    retry: false,
    onSuccess: () => { setTaskForAssignment(null); setSelectedEmployeeShiftId(undefined); refreshTasks(); message.success('Đã cập nhật phân công.') },
    onError: (error) => message.error(error.response?.data?.message || 'Phân công chưa được lưu. Hãy làm mới danh sách.'),
  })
  const cancel = useMutation({
    mutationFn: ({ id, payload }) => operationalTaskService.cancel(id, payload),
    retry: false,
    onSuccess: refreshTasks,
    onError: (error) => message.error(error.response?.data?.message || 'Không thể hủy công việc.'),
  })
  const createRecurrence = useMutation({
    mutationFn: operationalTaskService.createRecurrence,
    retry: false,
    onSuccess: () => { recurrenceForm.resetFields(); queryClient.invalidateQueries({ queryKey: ['operational-task-recurrences'] }); message.success('Đã lưu lịch đo định kỳ.') },
    onError: (error) => message.error(error.response?.data?.message || 'Không thể lưu lịch đo định kỳ.'),
  })
  const stopRecurrence = useMutation({
    mutationFn: operationalTaskService.stopRecurrence,
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['operational-task-recurrences'] }),
    onError: (error) => message.error(error.response?.data?.message || 'Không thể dừng lịch đo.'),
  })

  const openAssignment = (task) => {
    setTaskForAssignment(task)
    setSelectedEmployeeShiftId(task.employeeShiftId || undefined)
  }

  const submitAssignment = () => {
    const candidate = (assignees.data || []).find((item) => item.employeeShiftId === selectedEmployeeShiftId)
    if (!taskForAssignment || !candidate) return
    assign.mutate({
      id: taskForAssignment.id,
      payload: { taskId: taskForAssignment.id, employeeId: candidate.employeeId, employeeShiftId: candidate.employeeShiftId, expectedRevision: taskForAssignment.revision },
    })
  }

  const cancelTask = (task) => Modal.confirm({
    title: 'Hủy công việc này?',
    content: `Công việc ${taskTypeLabel(task.taskType).toLowerCase()} ngày ${displayDate(task.dutyDate)} sẽ được giữ trong lịch sử.`,
    okText: 'Hủy công việc', cancelText: 'Quay lại', autoFocusButton: 'cancel',
    onOk: () => cancel.mutate({ id: task.id, payload: { taskId: task.id, expectedRevision: task.revision } }),
  })

  const openResult = useCallback((task) => {
    if (task.dailyInspectionId) {
      setChecklistResultTask(task)
      return
    }
    if (task.waterQualityLogId) navigate(`/admin/water-quality?logId=${task.waterQualityLogId}`)
  }, [navigate])

  const openChecklist = useCallback((task, taskWeek) => {
      const shiftId = task.inspectionWindow === 'MORNING'
        ? FIXED_INSPECTION_SHIFT_IDS.morning
        : FIXED_INSPECTION_SHIFT_IDS.afternoon
      const returnParams = new URLSearchParams(searchParams)
      returnParams.delete('taskId')
      returnParams.set('week', weekMonday(taskWeek).format('YYYY-MM-DD'))
      if (view === 'list') returnParams.delete('view')
      else returnParams.set('view', view)
      const params = new URLSearchParams({ taskId: task.id, shiftId, returnTo: `/admin/staff/tasks?${returnParams.toString()}` })
      navigate(`/admin/equipments/daily-inspections?${params}`, {
        state: { returnTo: `/admin/staff/tasks?${returnParams.toString()}`, task },
      })
  }, [navigate, searchParams, view])

  const openTask = useCallback((task, taskWeek = weekStart) => {
    if (task.status === 'Cancelled') return
    if (terminal(task.status)) {
      openResult(task)
      return
    }
    if (!isAdmin && (!isLifeguard || task.assignedEmployeeId !== session?.id)) return
    if (task.taskType === 'EquipmentChecklist') {
      openChecklist(task, taskWeek)
      return
    }
    setMeasurementTask(task)
  }, [isAdmin, isLifeguard, session?.id, openResult, openChecklist, weekStart])

  useEffect(() => {
    if (!taskId) {
      handledDeepLink.current = null
      return
    }
    if (!linkedTask || handledDeepLink.current === taskId) return
    handledDeepLink.current = taskId
    const resolvedWeek = weekMonday(linkedTask.dutyDate)
    const nextSearchParams = new URLSearchParams(searchParams)
    nextSearchParams.set('week', resolvedWeek.format('YYYY-MM-DD'))
    if (linkedTask.taskType === 'EquipmentChecklist' && !terminal(linkedTask.status)) {
      openChecklist(linkedTask, resolvedWeek)
      return
    }
    if (terminal(linkedTask.status)) {
      if (linkedTask.status === 'Completed' && linkedTask.waterQualityLogId) {
        navigate(`/admin/water-quality?logId=${linkedTask.waterQualityLogId}`, { replace: true })
        return
      }
      if (!linkedTask.dailyInspectionId) nextSearchParams.delete('taskId')
    }
    setSearchParams(nextSearchParams, { replace: true })
  }, [linkedTask, navigate, openChecklist, searchParams, setSearchParams, taskId])

  const closeMeasurement = () => {
    setMeasurementTask(null)
    if (taskId) updateSearch((params) => {
      params.delete('taskId')
      params.set('week', weekStartKey)
    })
  }

  const renderActions = (task) => (
    <Space wrap>
      {(isAdmin || (isLifeguard && task.assignedEmployeeId === session?.id)) && !terminal(task.status) && (
        <Button type="primary" size="small" onClick={() => openTask(task)}>
          {task.taskType === 'Cleaning' ? 'Đo kiểm tra lại' : task.taskType === 'Measurement' ? 'Nộp kết quả đo' : 'Thực hiện checklist'}
        </Button>
      )}
      {isAdmin && !terminal(task.status) && <>
        <Button size="small" onClick={() => openAssignment(task)}>{task.assignedEmployeeId ? 'Đổi người' : 'Phân công'}</Button>
        <Button danger size="small" onClick={() => cancelTask(task)}>Hủy</Button>
      </>}
      {(task.dailyInspectionId || task.waterQualityLogId) && <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => openResult(task)}>Xem kết quả</Button>}
    </Space>
  )

  const renderWeeklyTask = (task) => {
    const [statusLabel, statusColor] = taskStatus(task.status)
    const resultStatus = task.dailyInspectionStatus || task.measurementStatus
    const resultLabel = ({ Passed: 'Đạt chuẩn', HasIssues: 'Có sự cố', Resolved: 'Đã xử lý', InResolution: 'Đang xử lý', WithinLimits: 'Đạt chuẩn', Warning: 'Cảnh báo', Danger: 'Nguy hiểm' })[resultStatus]
    const assigneeLabel = task.assignedEmployeeId
      ? task.assignedEmployeeName || 'Nhân viên'
      : task.unassignedReason === 'NO_ELIGIBLE_WORK_SHIFT' ? 'Chưa có ca phù hợp' : 'Chưa phân công'
    const action = (canExecute) => (
      <Space wrap size={4}>
        {canExecute && !terminal(task.status) && <Button type="primary" size="small" onClick={() => openTask(task)}>Mở</Button>}
        {isAdmin && !terminal(task.status) && <>
          <Button size="small" onClick={() => openAssignment(task)}>{task.assignedEmployeeId ? 'Đổi' : 'Gán'}</Button>
          <Button danger size="small" onClick={() => cancelTask(task)}>Hủy</Button>
        </>}
        {(task.dailyInspectionId || task.waterQualityLogId) && <Button type="link" size="small" aria-label="Xem kết quả" icon={<EyeOutlined />} onClick={() => openResult(task)} />}
      </Space>
    )

    return (
      <div key={task.id} className="staff-week-table-task" data-task-id={task.id}>
        <Button type="link" className="staff-task-name" onClick={() => openTask(task)} disabled={task.status === 'Cancelled'}>
          {taskTypeLabel(task.taskType)}
        </Button>
        <Tag color={statusColor}>{statusLabel}</Tag>
        <Typography.Text type="secondary" title={task.scheduledAtUtc ? displayTime(task.scheduledAtUtc) : task.dueAtUtc ? displayTime(task.dueAtUtc) : undefined}>
          {task.scheduledAtUtc ? displayTime(task.scheduledAtUtc) : task.dueAtUtc ? `Hạn ${displayTime(task.dueAtUtc)}` : 'Chưa có hạn'}
        </Typography.Text>
        <Typography.Text type="secondary" title={assigneeLabel}>{assigneeLabel}</Typography.Text>
        {resultLabel && <Tag>{resultLabel}</Tag>}
        {action(isAdmin || (isLifeguard && task.assignedEmployeeId === session?.id))}
      </div>
    )
  }

  const taskItems = useMemo(() => tasks.data?.items || [], [tasks.data])
  const weekDays = useMemo(() => Array.from({ length: 7 }, (_, index) => {
    const date = weekStart.add(index, 'day')
    const dateKey = date.format('YYYY-MM-DD')
    const dayTasks = taskItems
      .filter((task) => task.dutyDate === dateKey)
      .sort((left, right) => (left.scheduledAtUtc || left.dueAtUtc || '').localeCompare(right.scheduledAtUtc || right.dueAtUtc || ''))
    return { dateKey, title: vietnamDate(dateKey), tasks: dayTasks }
  }), [taskItems, weekStart])
  const overdueItems = overdueTasks.data?.items || []
  const recurrenceItems = recurrences.data || []

  const renderTask = (task) => {
    const [statusLabel, statusColor] = taskStatus(task.status)
    const resultStatus = task.dailyInspectionStatus || task.measurementStatus
    const resultLabel = ({
      Passed: 'Đạt chuẩn', HasIssues: 'Có sự cố', Resolved: 'Đã khắc phục', InResolution: 'Đang khắc phục',
      WithinLimits: 'Đạt chuẩn', Warning: 'Cảnh báo', Danger: 'Nguy hiểm',
    })[resultStatus]
    const assigneeLabel = task.assignedEmployeeId
      ? task.assignedEmployeeName || 'Nhân viên'
      : task.unassignedReason === 'NO_ELIGIBLE_WORK_SHIFT' ? 'Chưa có ca phù hợp' : 'Chưa phân công'

    return (
      <article key={task.id} className="staff-week-shift-card" data-task-id={task.id}>
        <div className="staff-week-shift-heading">
          <Button type="link" className="staff-task-name" onClick={() => openTask(task)} disabled={task.status === 'Cancelled'}>
            {taskTypeLabel(task.taskType)}
          </Button>
          <Tag color={statusColor}>{statusLabel}</Tag>
          <Typography.Text type="secondary">
            {task.inspectionWindow === 'MORNING' ? 'Ca sáng' : task.inspectionWindow === 'AFTERNOON' ? 'Ca chiều' : task.shiftName || 'Chưa xác định ca'}
          </Typography.Text>
        </div>
        <Descriptions size="small" column={1}>
          <Descriptions.Item label="Thời gian">
            {task.scheduledAtUtc ? displayTime(task.scheduledAtUtc) : 'Chưa xác định'}
          </Descriptions.Item>
          <Descriptions.Item label="Ngày">{displayDate(task.dutyDate)}</Descriptions.Item>
          {task.dueAtUtc && <Descriptions.Item label="Hạn hoàn thành">{displayDate(task.dueAtUtc)} · {displayTime(task.dueAtUtc)}</Descriptions.Item>}
          <Descriptions.Item label="Nhân viên">{assigneeLabel}</Descriptions.Item>
          {resultLabel && <Descriptions.Item label="Kết quả">{resultLabel}{task.dailyInspectionAt && ` · ${displayTime(task.dailyInspectionAt)}`}{task.measuredAt && ` · ${displayTime(task.measuredAt)}`}</Descriptions.Item>}
        </Descriptions>
        {task.unassignedReason && !task.assignedEmployeeId && (
          <Typography.Text type="secondary">
            {task.unassignedReason === 'NO_ELIGIBLE_WORK_SHIFT' ? 'Chưa có ca làm phù hợp.' : 'Cần quản trị viên phân công.'}
          </Typography.Text>
        )}
        <div className="staff-schedule-header-actions">{renderActions(task)}</div>
      </article>
    )
  }

  const renderDay = (day) => (
    <section key={day.dateKey} className="staff-week-day" aria-label={`Công việc ${day.title}`}>
      <header>
        <strong>{day.title}</strong>
        <Typography.Text type="secondary">{day.tasks.length} công việc</Typography.Text>
      </header>
      {day.tasks.length > 0
        ? day.tasks.map(renderTask)
        : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có công việc" />}
    </section>
  )

  const weeklyColumns = weekDays.map((day) => ({
    title: <div><strong>{dayjs(day.dateKey).locale('vi').format('ddd')}</strong><br /><Typography.Text type="secondary">{dayjs(day.dateKey).format('DD/MM')} · {day.tasks.length}</Typography.Text></div>,
    key: day.dateKey,
    width: 152,
    render: () => day.tasks.length > 0
      ? <div className="staff-week-table-task-list">{day.tasks.map(renderWeeklyTask)}</div>
      : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có công việc" />,
  }))

  const retryEnsureWeek = () => ensureWeekMutate(weekStartKey)
  const deepLinkMissing = Boolean(taskId && taskLookup.isSuccess && !linkedTask)

  if (!isAdmin && session?.role?.toUpperCase() !== 'LIFEGUARD') {
    return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ quản trị viên và nhân viên cứu hộ được xem công việc." /></div>
  }

  return (
    <div className="service-list-page staff-schedule-page staff-operational-tasks">
      <ServiceListHeader
        title="Công việc vận hành"
        action={<div className="staff-schedule-header-actions">
          {isAdmin && <Button icon={<ScheduleOutlined />} onClick={() => setRecurrenceOpen(true)}>Lịch đo định kỳ</Button>}
          {isAdmin && <Button icon={<CalendarOutlined />} loading={ensureWeek.isPending} onClick={retryEnsureWeek}>Chuẩn bị tuần</Button>}
          <Button icon={<ReloadOutlined />} loading={tasks.isFetching || taskLookup.isFetching} onClick={() => taskId ? taskLookup.refetch() : tasks.refetch()}>Làm mới</Button>
        </div>}
      />
      <section className="service-list-toolbar staff-schedule-toolbar" aria-label="Bộ lọc công việc">
        <Space className="staff-schedule-filters" wrap size={[12, 8]}>
          <div className="staff-operational-week-controls" aria-label="Chọn tuần">
            <Button icon={<LeftOutlined />} aria-label="Tuần trước" onClick={() => selectWeek(weekStart.subtract(7, 'day'))} />
            <Typography.Text strong>{displayDate(weekStartKey)} – {displayDate(weekEndKey)}</Typography.Text>
            <Button className="staff-task-next-week" icon={<RightOutlined />} aria-label="Tuần tiếp theo" onClick={() => selectWeek(weekStart.add(7, 'day'))} />
            <Button className="staff-operational-week-current" type="link" onClick={() => selectWeek(weekMonday(todayVietnam()), true)}>Tuần hiện tại</Button>
          </div>
          <DatePicker
              picker="week" locale={viDatePicker.DatePicker} value={weekStart.locale('vi')} allowClear={false}
            aria-label="Chọn tuần công việc" format="DD/MM/YYYY"
            onChange={(date) => date && selectWeek(date)}
          />
          <Space wrap size={[12, 8]}>
            <Select
              allowClear placeholder="Loại công việc" style={{ width: 210 }} value={taskTypeFilter}
              options={[{ value: 'Measurement', label: 'Đo chất lượng nước' }, { value: 'EquipmentChecklist', label: 'Kiểm tra thiết bị' }, { value: 'Cleaning', label: 'Xử lý chất lượng nước' }]}
              onChange={(taskType) => updateFilter('taskType', taskType)}
            />
            <Select
              allowClear placeholder="Trạng thái" style={{ width: 170 }} value={statusFilter}
              options={[{ value: 'Scheduled', label: 'Đã lên lịch' }, { value: 'Unassigned', label: 'Chưa phân công' }, { value: 'InProgress', label: 'Đang xử lý' }, { value: 'Completed', label: 'Hoàn tất' }, { value: 'Absent', label: 'Vắng mặt' }, { value: 'Cancelled', label: 'Đã hủy' }]}
              onChange={(status) => updateFilter('status', status)}
            />
            {isAdmin && <Select
              allowClear showSearch optionFilterProp="label" placeholder="Nhân viên" style={{ width: 220 }} value={employeeFilter}
              options={roster.map((item) => ({ value: item.employeeId, label: item.name }))}
              onChange={(employeeId) => updateFilter('employeeId', employeeId)}
            />}
          </Space>
          <div className="staff-schedule-view-control">
            <span>Hiển thị</span>
            <Segmented
              className="staff-schedule-view-switch"
              aria-label="Cách hiển thị công việc"
              value={view}
              onChange={selectView}
              options={[{ label: 'Danh sách', value: 'list' }, { label: 'Bảng tuần', value: 'table' }]}
            />
          </div>
        </Space>
      </section>

      <section className="service-list-table staff-schedule-table" aria-label="Danh sách công việc">
        {ensureWeek.isError && isAdmin && <Alert
          className="staff-schedule-feedback"
          type="warning"
          showIcon
          message="Chưa thể chuẩn bị công việc cho tuần này."
          description={ensureWeek.error?.response?.data?.message || 'Thử chuẩn bị tuần lại. Công việc hiện có vẫn được giữ.'}
          action={<Button size="small" onClick={retryEnsureWeek}>Thử lại</Button>}
        />}
        {tasks.isError || (taskId && taskLookup.isError) ? <Alert
          className="staff-schedule-feedback"
          type="error"
          showIcon
          message="Không thể tải công việc."
           description={tasks.error?.response?.data?.message || taskLookup.error?.response?.data?.message || 'Hãy làm mới danh sách.'}
        /> : deepLinkMissing ? <Alert type="warning" showIcon message="Không tìm thấy công việc được yêu cầu." /> : tasks.isLoading || (taskId && taskLookup.isLoading) || ensureWeek.isPending ? (
          <div className="staff-week-loading"><Spin tip="Đang tải công việc trong tuần..." /></div>
        ) : <>
          <div className="staff-schedule-header-actions staff-overdue-link">
            {overdueTasks.isError ? <Alert type="error" showIcon message="Không thể tải công việc quá hạn." /> : (
              <Button type="link" loading={overdueTasks.isLoading} onClick={() => setShowOverdue(true)}>
                Công việc đang mở trước tuần này ({overdueTasks.data?.totalCount ?? 0})
              </Button>
            )}
          </div>
          {view === 'table' && screens.lg ? (
            <Table
              className="staff-week-table"
              rowKey="key"
              columns={weeklyColumns}
              dataSource={[{ key: weekStartKey }]}
              pagination={false}
              scroll={{ x: 'max-content' }}
            />
          ) : (
            <div className="staff-week-board staff-week-board--stacked">
              {weekDays.map(renderDay)}
            </div>
          )}
        </>}
      </section>

      <Modal
        className="staff-operational-task-modal"
        title={`Công việc đang mở trước tuần ${displayDate(weekStartKey)}`}
        open={showOverdue}
        onCancel={() => setShowOverdue(false)}
        footer={null}
        width={860}
      >
        {overdueTasks.isError ? <Alert type="error" showIcon message="Không thể tải danh sách công việc quá hạn." /> : overdueTasks.isLoading ? (
          <div className="staff-week-loading"><Spin tip="Đang tải công việc..." /></div>
        ) : overdueItems.length === 0 ? <Empty description="Không còn công việc đang mở trước tuần này." /> : (
          <div className="staff-week-board staff-week-board--stacked">
            {overdueItems.map(renderTask)}
          </div>
        )}
      </Modal>

      <Modal
        title="Chi tiết checklist đã hoàn thành"
        open={Boolean(checklistResultTask)}
        onCancel={() => {
          setChecklistResultTask(null)
          if (taskId) updateSearch((params) => {
            params.delete('taskId')
            params.set('week', weekStartKey)
          })
        }}
        footer={null}
        width={760}
      >
        {checklistResult.isError ? <Alert
          type="error"
          showIcon
          message="Không thể tải kết quả checklist."
          description={checklistResult.error?.response?.data?.message || 'Hãy làm mới danh sách công việc rồi thử lại.'}
          action={<Button size="small" onClick={() => checklistResult.refetch()}>Thử lại</Button>}
        /> : checklistResult.isLoading ? (
          <div className="staff-week-loading"><Spin tip="Đang tải kết quả checklist..." /></div>
        ) : checklistResult.data && <>
          <Descriptions size="small" column={{ xs: 1, sm: 2 }}>
            <Descriptions.Item label="Ngày">{displayDate(checklistResult.data.inspectionDate)}</Descriptions.Item>
            <Descriptions.Item label="Ca">{checklistResult.data.shiftName}</Descriptions.Item>
            <Descriptions.Item label="Người thực hiện">{checklistResult.data.inspectorName}</Descriptions.Item>
            <Descriptions.Item label="Kết quả">
              <Tag color={checklistResult.data.overallStatus === 'Passed' ? 'success' : 'warning'}>
                {checklistResult.data.overallStatus === 'Passed' ? 'Đạt chuẩn' : 'Có sự cố'}
              </Tag>
            </Descriptions.Item>
          </Descriptions>
          {checklistResult.data.notes && <Typography.Paragraph>{checklistResult.data.notes}</Typography.Paragraph>}
          <Table
            rowKey="equipmentId"
            size="small"
            pagination={false}
            scroll={{ x: 540 }}
            dataSource={checklistResult.data.items || []}
            columns={[
              { title: 'Thiết bị', dataIndex: 'equipmentName', key: 'equipmentName' },
              { title: 'Kết quả', key: 'result', render: (_, item) => <Tag color={item.isPassed ? 'success' : 'error'}>{item.isPassed ? 'Đạt chuẩn' : 'Có vấn đề'}</Tag> },
              { title: 'Ghi chú', dataIndex: 'note', key: 'note', render: (note) => note || '—' },
            ]}
            locale={{ emptyText: <Empty description="Không có thiết bị trong biên bản này." /> }}
          />
        </>}
      </Modal>

      <Modal
        title="Phân công công việc"
        open={Boolean(taskForAssignment)}
        onCancel={() => { setTaskForAssignment(null); setSelectedEmployeeShiftId(undefined) }}
        onOk={submitAssignment}
        okText="Lưu phân công"
        cancelText="Đóng"
        confirmLoading={assign.isPending}
        okButtonProps={{ disabled: !selectedEmployeeShiftId || assignees.isLoading || assignees.isError }}
      >
        {assignees.isError ? <Alert type="error" showIcon title="Không tải được ca đủ điều kiện" description="Làm mới trang rồi thử lại." /> : <>
          <Typography.Paragraph>Chọn nhân viên cứu hộ có ca làm phù hợp với thời gian công việc.</Typography.Paragraph>
          {assignees.data?.length === 0 && <Alert type="warning" showIcon message="Không có ca đủ điều kiện tại thời điểm này." />}
          <Select
            aria-label="Nhân viên và ca làm"
            loading={assignees.isLoading}
            value={selectedEmployeeShiftId}
            onChange={setSelectedEmployeeShiftId}
            style={{ width: '100%' }}
            placeholder="Chọn nhân viên và ca làm"
            options={(assignees.data || []).map((item) => ({ value: item.employeeShiftId, label: `${item.name} · ${item.shiftName} (${item.shiftStartTime}–${item.shiftEndTime})` }))}
          />
        </>}
      </Modal>

      <Modal
        title="Lịch đo chất lượng nước định kỳ"
        open={recurrenceOpen}
        onCancel={() => setRecurrenceOpen(false)}
        footer={null}
        width={760}
      >
        <Form
          form={recurrenceForm}
          layout="vertical"
          initialValues={{ anchorDate: dayjs(todayVietnam()), scheduledTime: dayjs().hour(8).minute(0).second(0) }}
          onFinish={(values) => createRecurrence.mutate({
            anchorDate: values.anchorDate.format('YYYY-MM-DD'),
            scheduledTime: values.scheduledTime.format('HH:mm:ss'),
            shiftId: values.shiftId,
            preferredEmployeeId: values.preferredEmployeeId,
          })}
        >
          <Space wrap align="start">
            <Form.Item name="anchorDate" label="Ngày bắt đầu" rules={[{ required: true, message: 'Chọn ngày bắt đầu.' }]}>
              <DatePicker format="DD/MM/YYYY" disabledDate={(date) => date.isBefore(dayjs(todayVietnam()), 'day')} />
            </Form.Item>
            <Form.Item name="scheduledTime" label="Giờ đo" rules={[{ required: true, message: 'Chọn giờ đo.' }]}>
              <TimePicker format="HH:mm" />
            </Form.Item>
            <Form.Item name="shiftId" label="Ca làm" rules={[{ required: true, message: 'Chọn ca làm.' }]}>
              <Select style={{ minWidth: 160 }} options={shiftOptions.map((item) => ({ value: item.id, label: item.name }))} />
            </Form.Item>
            <Form.Item name="preferredEmployeeId" label="Nhân viên cứu hộ" rules={[{ required: true, message: 'Chọn nhân viên.' }]}>
              <Select showSearch optionFilterProp="label" style={{ minWidth: 220 }} options={lifeguards.map((item) => ({ value: item.employeeId, label: item.name }))} />
            </Form.Item>
          </Space>
          <Button type="primary" htmlType="submit" loading={createRecurrence.isPending}>Tạo lịch lặp mỗi 3 ngày</Button>
        </Form>
        {recurrences.isError && <Alert className="staff-schedule-alert" type="error" showIcon title="Không tải được lịch đo lặp" />}
        <Table
          className="staff-schedule-alert"
          rowKey="id"
          size="small"
          dataSource={recurrenceItems}
          pagination={false}
          columns={[
            { title: 'Bắt đầu', dataIndex: 'anchorDate', render: displayDate },
            { title: 'Lặp tiếp theo', dataIndex: 'nextOccurrenceDate', render: displayDate },
            { title: 'Ca / giờ', render: (_, item) => `${item.shiftName} · ${item.scheduledTime}` },
            { title: 'Nhân viên', dataIndex: 'preferredEmployeeName' },
            { title: 'Trạng thái', dataIndex: 'isActive', render: (active) => <Tag color={active ? 'success' : 'default'}>{active ? 'Đang chạy' : 'Đã dừng'}</Tag> },
            { title: '', key: 'action', render: (_, item) => item.isActive && <Button type="link" danger onClick={() => stopRecurrence.mutate(item.id)}>Dừng</Button> },
          ]}
        />
      </Modal>

      <CreateWaterQualityLogModal
        key={measurementTask?.id || 'measurement-closed'}
        visible={Boolean(measurementTask)}
        operationalTaskId={measurementTask?.id}
        taskType={measurementTask?.taskType}
        adminTaskExecution={isAdmin && measurementTask ? {
          ...measurementTask,
          assignedEmployeeId: measurementTask.assignedEmployeeId ?? null,
          assignedEmployeeName: measurementTask.assignedEmployeeName || 'Chưa phân công',
          performerName: session?.fullName || session?.userName || 'Quản trị viên',
        } : null}
        onCancel={closeMeasurement}
        onSuccess={() => { closeMeasurement(); refreshTasks() }}
      />
    </div>
  )
}
