import { useRef, useState } from 'react'
import { Alert, Button, DatePicker, Form, Input, Select, Space, Spin, Table, Typography } from 'antd'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { useActiveShifts } from '../hooks/useShifts'
import { formatOnboardingDate, onboardingError, vietnamToday } from '../onboarding/onboardingUi'
import staffService from '../services/staffService'
import { workPatternService } from '../services/workPatternService'
import { shiftLabel } from '../contracts/contractUi'
import ContractTermsFields from './ContractTermsFields'
import ContractTermsSummary from './ContractTermsSummary'
import ScheduleOperationResult from './ScheduleOperationResult'

function toContract(contract) {
  return contract ? {
    ...contract,
    weeklyPattern: Array.from({ length: 7 }, (_, index) => ({
      dayOfWeek: index + 1,
      shiftIds: contract.weeklyPattern?.find((day) => day.dayOfWeek === index + 1)?.shiftIds || [],
    })),
  } : undefined
}

export default function StaffWorkPatternPanel({ employeeId }) {
  const [form] = Form.useForm()
  const cache = useQueryClient()
  const shiftsQuery = useActiveShifts(true)
  const viewQuery = useQuery({ queryKey: ['staff-work-pattern', employeeId], queryFn: () => workPatternService.get(employeeId), enabled: Boolean(employeeId), retry: false })
  const view = viewQuery.data
  const shifts = [...(shiftsQuery.data?.items || []), ...(view?.shifts || []).filter((shift) => !shiftsQuery.data?.items?.some((active) => active.id === shift.id))]
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [preview, setPreview] = useState(null)
  const [proposal, setProposal] = useState(null)
  const [pendingOperation, setPendingOperation] = useState(null)
  const [uncertain, setUncertain] = useState(false)
  const [kind, setKind] = useState('Leave')
  const [workDate, setWorkDate] = useState('')
  const [sourceIds, setSourceIds] = useState(null)
  const [destinationShiftId, setDestinationShiftId] = useState()
  const [reason, setReason] = useState('')
  const [exceptionPreview, setExceptionPreview] = useState(null)
  const [exceptionProposal, setExceptionProposal] = useState(null)
  const guard = useRef(false)

  const refresh = async () => {
    setError('')
    try {
      const refreshed = await viewQuery.refetch()
      if (!refreshed.data) throw refreshed.error || new Error('Không thể tải hợp đồng và lịch tuần.')
      form.setFieldsValue({ contract: toContract(refreshed.data.contract) })
      await Promise.all([cache.invalidateQueries({ queryKey: ['staff-schedule-page'] }), cache.invalidateQueries({ queryKey: ['staff-schedule', employeeId] })])
      setPreview(null)
      setProposal(null)
      setPendingOperation(null)
      setUncertain(false)
      return refreshed.data
    } catch (failure) {
      setError(onboardingError(failure))
      return null
    }
  }

  const assignmentsQuery = useQuery({
    queryKey: ['staff-schedule', employeeId, { date: workDate, pageIndex: 1, pageSize: 100 }],
    queryFn: () => staffService.getSchedule({ employeeId, date: workDate, pageIndex: 1, pageSize: 100 }),
    enabled: Boolean(workDate), retry: false,
  })
  const assignments = (assignmentsQuery.data?.items || []).filter((item) => item.status === 'Scheduled')
  const assignmentError = assignmentsQuery.isError ? onboardingError(assignmentsQuery.error) : ''
  const defaultSourceIds = kind === 'Leave' ? assignments.filter((item) => item.isWorkPatternGenerated).map((item) => item.id) : []
  const selectedSourceIds = sourceIds ?? defaultSourceIds

  const mutate = async (operation, after, { allowUncertain = false, markUncertain = true } = {}) => {
    if (guard.current || (uncertain && !allowUncertain)) return false
    guard.current = true
    setBusy(true)
    setError('')
    try {
      const result = await operation()
      after?.(result)
      setUncertain(false)
      return true
    } catch (failure) {
      setError(onboardingError(failure))
      if (!failure.response && markUncertain) setUncertain(true)
      return false
    } finally {
      guard.current = false
      setBusy(false)
    }
  }

  const previewPattern = async () => {
    try {
      const values = await form.validateFields()
      const current = view?.contract
      const contract = { ...values.contract, weeklyPattern: values.contract.weeklyPattern }
      const payload = { contract, effectiveFrom: values.effectiveFrom.format('YYYY-MM-DD'), expectedVersion: view?.version ?? 0 }
      setPreview(null)
      setProposal(payload)
      await mutate(() => workPatternService.preview(employeeId, payload), (result) => setPreview(result), { markUncertain: false })
      if (!current && !contract.startDate) setProposal(null)
    } catch (failure) {
      if (failure?.errorFields) return
      setError(onboardingError(failure))
    }
  }

  const confirmPattern = async () => {
    if (!preview?.previewToken || !proposal) return
    const operationId = pendingOperation?.operationId || crypto.randomUUID()
    const payload = { ...proposal, previewToken: preview.previewToken, operationId }
    setPendingOperation(payload)
    await mutate(() => workPatternService.confirm(employeeId, payload), async (result) => {
      setPreview(null)
      setProposal(null)
      setPendingOperation(null)
      await refresh()
      setPreview(result.result)
    })
  }

  const resolveOperation = async () => {
    if (!pendingOperation?.operationId) return
    await mutate(() => workPatternService.operation(employeeId, pendingOperation.operationId), async (result) => {
      setUncertain(false)
      setPendingOperation(null)
      await refresh()
      setPreview(result.result)
    }, { allowUncertain: true })
  }

  const previewDatedException = async () => {
    if (!view || !workDate || !reason.trim() || selectedSourceIds.length === 0 || (kind === 'Move' && (!destinationShiftId || selectedSourceIds.length !== 1))) {
      setError('Chọn ngày, lịch nguồn, lý do và ca đích cho thao tác chuyển ca.')
      return
    }
    const payload = { kind, workDate, sourceAssignmentIds: selectedSourceIds, destinationShiftId: kind === 'Move' ? destinationShiftId : null,
      reason: reason.trim(), expectedVersion: view.version }
    setExceptionPreview(null)
    setExceptionProposal(payload)
    await mutate(() => workPatternService.previewException(employeeId, payload), (result) => setExceptionPreview(result), { markUncertain: false })
  }

  const confirmDatedException = async () => {
    if (!exceptionPreview?.previewToken || !exceptionProposal) return
    const operationId = pendingOperation?.operationId || crypto.randomUUID()
    const payload = { ...exceptionProposal, previewToken: exceptionPreview.previewToken, operationId }
    setPendingOperation(payload)
    await mutate(() => workPatternService.confirmException(employeeId, payload), async (result) => {
      setExceptionPreview(null)
      setExceptionProposal(null)
      setPendingOperation(null)
      await refresh()
      setExceptionPreview(result.result)
    })
  }

  const shiftOptions = (shiftsQuery.data?.items || []).map((shift) => ({ value: shift.id, label: `${shift.name} · ${shift.startTime?.slice(0, 5)}–${shift.endTime?.slice(0, 5)}` }))
  const scheduleOptions = assignments.filter((item) => kind !== 'Leave' || item.isWorkPatternGenerated)
    .map((item) => ({ value: item.id, label: `${item.shiftName} · ${item.startTime}–${item.endTime}${item.isWorkPatternGenerated ? ' · Hợp đồng' : ' · Điều chỉnh riêng'}` }))

  return <div className="staff-contract-stack">
    {viewQuery.isError && <Alert type="error" showIcon title="Không thể tải hợp đồng và lịch tuần" description={onboardingError(viewQuery.error)} action={<Button size="small" onClick={() => viewQuery.refetch()}>Thử lại</Button>} />}
    {error && <Alert type="error" showIcon title="Không thể hoàn tất thao tác" description={error} />}
    {uncertain && <Alert type="warning" showIcon title="Kết quả chưa xác định" description="Không gửi lại thao tác ngay. Kiểm tra kết quả bằng mã thao tác trước khi thử lại." action={<Button size="small" onClick={resolveOperation}>Kiểm tra kết quả</Button>} />}
    {(!view || shiftsQuery.isLoading) && !error && !viewQuery.isError && <Spin aria-label="Đang tải hợp đồng và lịch tuần" />}
    {shiftsQuery.isError && <Alert type="error" showIcon title="Không thể tải ca làm việc" description="Thử lại trước khi chỉnh sửa hợp đồng." action={<Button size="small" onClick={() => shiftsQuery.refetch()}>Thử lại</Button>} />}
    {view && <>
      {view.contract && <ContractTermsSummary contract={view.contract} shifts={view.shifts || []} />}
      <Form form={form} layout="vertical" className="staff-contract-stack" disabled={busy || uncertain || shiftsQuery.isLoading || shiftsQuery.isError} initialValues={{ contract: toContract(view.contract), effectiveFrom: dayjs(vietnamToday()).add(1, 'day') }} onValuesChange={() => { setPreview(null); setProposal(null); setPendingOperation(null) }}>
        <ContractTermsFields form={form} shifts={shifts} />
        <Form.Item name="effectiveFrom" label="Áp dụng từ ngày" rules={[{ required: true, message: 'Chọn ngày bắt đầu áp dụng.' }, { validator: (_, value) => !value || value.format('YYYY-MM-DD') > vietnamToday() ? Promise.resolve() : Promise.reject(new Error('Thay đổi thường kỳ chỉ áp dụng từ ngày mai trở đi.')) }]}>
          <DatePicker format="DD/MM/YYYY" disabledDate={(date) => date.format('YYYY-MM-DD') <= vietnamToday()} />
        </Form.Item>
      </Form>
      <Space wrap><Button type="primary" disabled={busy || uncertain || shiftsQuery.isError} loading={busy} onClick={previewPattern}>Xem trước thay đổi</Button><Button disabled={busy || uncertain} onClick={refresh}>Tải lại lịch sử</Button></Space>
      {preview && <><ScheduleOperationResult result={preview} preview={!preview.operationId} />
        {preview.previewToken && <Button type="primary" disabled={busy || uncertain} loading={busy} onClick={confirmPattern}>Xác nhận lịch thường kỳ</Button>}</>}
      <Typography.Title level={5}>Lịch sử phiên bản</Typography.Title>
      <Table rowKey="id" size="small" pagination={{ pageSize: 5 }} dataSource={view.history || []} scroll={{ x: 680 }} columns={[
        { title: 'Phiên bản', dataIndex: 'version' }, { title: 'Áp dụng từ', dataIndex: 'effectiveFrom', render: (value) => formatOnboardingDate(value) },
        { title: 'Bắt đầu hợp đồng', render: (_, item) => formatOnboardingDate(item.contract.startDate) }, { title: 'Kết thúc hợp đồng', render: (_, item) => formatOnboardingDate(item.contract.endDate) },
      ]} />
      <Typography.Title level={5}>Ngoại lệ theo ngày</Typography.Title>
      <Form layout="vertical" disabled={busy || uncertain}>
        <Form.Item label="Loại ngoại lệ"><Select value={kind} onChange={(value) => { setKind(value); setSourceIds(null); setExceptionPreview(null); setExceptionProposal(null); setPendingOperation(null) }} options={[{ value: 'Leave', label: 'Nghỉ trong một ngày' }, { value: 'Move', label: 'Chuyển ca trong một ngày' }]} /></Form.Item>
        <Form.Item label="Ngày làm"><DatePicker value={workDate ? dayjs(workDate) : null} onChange={(value) => { setWorkDate(value?.format('YYYY-MM-DD') || ''); setSourceIds(null); setExceptionPreview(null); setExceptionProposal(null); setPendingOperation(null) }} format="DD/MM/YYYY" /></Form.Item>
        {assignmentError && <Alert type="error" showIcon title="Không thể tải lịch nguồn" description={assignmentError} />}
        {workDate && kind === 'Leave' && !assignmentError && !scheduleOptions.length && <Alert type="info" showIcon title="Không có ca theo hợp đồng trong ngày này" description="Ngoại lệ nghỉ chỉ hủy các ca đã sinh từ lịch hợp đồng. Lịch điều chỉnh riêng không đổi." />}
        <Form.Item label={kind === 'Leave' ? 'Ca bị nghỉ' : 'Ca cần chuyển'}>
          <Select mode={kind === 'Leave' ? 'multiple' : undefined} value={kind === 'Leave' ? selectedSourceIds : selectedSourceIds[0]} onChange={(value) => { setSourceIds(kind === 'Leave' ? value : value ? [value] : []); setExceptionPreview(null); setExceptionProposal(null); setPendingOperation(null) }} options={scheduleOptions} disabled={!workDate || assignmentError} placeholder="Chọn ca đã lên lịch" />
        </Form.Item>
        {kind === 'Move' && <Form.Item label="Ca đích"><Select value={destinationShiftId} onChange={(value) => { setDestinationShiftId(value); setExceptionPreview(null); setExceptionProposal(null); setPendingOperation(null) }} options={shiftOptions} placeholder="Chọn ca đang hoạt động" /></Form.Item>}
        <Form.Item label="Lý do"><Input.TextArea rows={3} maxLength={1000} value={reason} onChange={(event) => { setReason(event.target.value); setExceptionPreview(null); setExceptionProposal(null); setPendingOperation(null) }} /></Form.Item>
      </Form>
      <Space wrap><Button disabled={busy || uncertain} onClick={previewDatedException}>Xem trước ngoại lệ</Button>{exceptionPreview?.previewToken && <Button type="primary" disabled={busy || uncertain} loading={busy} onClick={confirmDatedException}>Xác nhận ngoại lệ</Button>}</Space>
      {exceptionPreview && <ScheduleOperationResult result={exceptionPreview} preview={!exceptionPreview.operationId} />}
      <Table rowKey="id" size="small" pagination={{ pageSize: 5 }} dataSource={view.exceptions || []} scroll={{ x: 620 }} columns={[
        { title: 'Loại', dataIndex: 'kind', render: (value) => value === 'Leave' ? 'Nghỉ trong ngày' : value === 'Move' ? 'Chuyển ca trong ngày' : value === 'Replace' ? 'Thay nhân viên' : value }, { title: 'Ngày', dataIndex: 'workDate', render: (value) => formatOnboardingDate(value) }, { title: 'Nhân viên thay thế', dataIndex: 'destinationEmployeeName', render: (value, item) => item.kind === 'Replace' ? value || 'Không có thông tin' : '-' }, { title: 'Lý do', dataIndex: 'reason' }, { title: 'Ca đích', dataIndex: 'destinationShiftId', render: (value) => { const shift = shifts.find((item) => item.id === value); return shift ? shiftLabel(shift) : value ? 'Chưa có thông tin ca' : '-' } },
      ]} />
      <Typography.Title level={5}>Trạng thái gửi email lịch</Typography.Title>
      <Table rowKey="id" size="small" pagination={{ pageSize: 5 }} dataSource={view.deliveries || []} scroll={{ x: 560 }} columns={[
        { title: 'Tạo lúc', dataIndex: 'createdAtUtc', render: (value) => value ? new Date(value).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' }) : '-' },
        { title: 'Kênh', dataIndex: 'channel' }, { title: 'Trạng thái', dataIndex: 'status' },
        { title: 'Lần thử', dataIndex: 'attemptCount' }, { title: 'Mã lỗi', dataIndex: 'lastErrorCode', render: (value) => value || '-' },
      ]} />
    </>}
  </div>
}
