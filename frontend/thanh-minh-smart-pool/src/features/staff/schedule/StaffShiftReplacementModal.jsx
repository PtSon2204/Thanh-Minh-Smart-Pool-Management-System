import { useState } from 'react'
import { Alert, Button, Descriptions, Form, Input, Modal, Select, Space, Tag, Typography } from 'antd'
import { formatDate, formatTime } from './scheduleUtils'
import { useReplaceSchedule } from '../hooks/useStaffs'
import staffService from '../services/staffService'

function errorMessage(error) {
  const code = error?.response?.data?.code
  const messages = {
    STALE_PREVIEW: 'Lịch đã đổi sau khi xem trước. Hãy xem trước lại rồi xác nhận.',
    OPERATION_NOT_FOUND: 'Chưa tìm thấy biên nhận đã lưu. Không gửi lại thao tác; hãy kiểm tra kết quả sau.',
    OPERATION_PAYLOAD_MISMATCH: 'Mã thao tác đã gắn với dữ liệu khác. Tải lại lịch trước khi tiếp tục.',
    INVALID_REPLACEMENT: 'Chọn nhân viên thay thế và nhập lý do hợp lệ.',
    OPERATION_ID_REQUIRED: 'Thiếu mã thao tác. Hãy xem trước lại rồi thử xác nhận.',
  }
  return error?.response?.data?.message || messages[code] || code || 'Không thể hoàn tất thao tác thay ca.'
}

export default function StaffShiftReplacementModal({ assignment, employees, onClose, onCompleted, onBusyChange, onUncertainChange }) {
  const [replacementEmployeeId, setReplacementEmployeeId] = useState()
  const [reason, setReason] = useState('')
  const [proposal, setProposal] = useState(null)
  const [preview, setPreview] = useState(null)
  const [pendingOperation, setPendingOperation] = useState(null)
  const [uncertain, setUncertain] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const replacement = useReplaceSchedule()
  const candidates = employees.filter((employee) => employee.employeeId !== assignment.employeeId)

  const setRequestBusy = (value) => {
    setBusy(value)
    onBusyChange?.(value)
  }
  const setResultUncertain = (value) => {
    setUncertain(value)
    onUncertainChange?.(value)
  }

  const invalidatePreview = () => {
    setPreview(null)
    setProposal(null)
    setPendingOperation(null)
    setError('')
  }

  const previewReplacement = async () => {
    if (busy || uncertain) return
    if (!replacementEmployeeId || !reason.trim()) {
      setError('Chọn nhân viên thay thế và nhập lý do.')
      return
    }
    const payload = { replacementEmployeeId, reason: reason.trim() }
    setRequestBusy(true)
    setError('')
    setProposal(payload)
    try {
      setPreview(await staffService.previewScheduleReplacement(assignment.id, payload))
    } catch (failure) {
      setError(errorMessage(failure))
    } finally {
      setRequestBusy(false)
    }
  }

  const confirmReplacement = async () => {
    if (!preview?.previewToken || !proposal || busy || uncertain) return
    const operation = {
      ...proposal,
      previewToken: preview.previewToken,
      operationId: pendingOperation?.operationId || crypto.randomUUID(),
    }
    setPendingOperation(operation)
    setRequestBusy(true)
    setError('')
    try {
      const result = await replacement.mutateAsync({ assignmentId: assignment.id, data: operation })
      setResultUncertain(false)
      await onCompleted(result)
    } catch (failure) {
      if (!failure.response) {
        setResultUncertain(true)
        setError('Kết quả chưa xác định. Kiểm tra mã thao tác trước khi thử lại.')
      } else {
        if (failure.response.data?.code === 'STALE_PREVIEW') invalidatePreview()
        setError(errorMessage(failure))
      }
    } finally {
      setRequestBusy(false)
    }
  }

  const resolveOperation = async () => {
    if (!pendingOperation?.operationId || busy) return
    setRequestBusy(true)
    setError('')
    try {
      const result = await staffService.getScheduleReplacementOperation(pendingOperation.operationId)
      setResultUncertain(false)
      await onCompleted(result)
    } catch (failure) {
      setError(failure.response?.data?.code === 'OPERATION_NOT_FOUND'
        ? 'Chưa có biên nhận đã lưu. Không gửi lại thao tác; hãy kiểm tra kết quả sau.'
        : errorMessage(failure))
    } finally {
      setRequestBusy(false)
    }
  }

  return <Modal
    open
    title="Thay nhân viên cho ca"
    onCancel={onClose}
    closable={!busy && !uncertain}
    maskClosable={!busy && !uncertain}
    keyboard={!busy && !uncertain}
    styles={{ body: { maxHeight: '60dvh', overflowY: 'auto' } }}
    footer={<Space wrap>
      {uncertain && <Button onClick={resolveOperation} loading={busy}>Kiểm tra kết quả</Button>}
      <Button onClick={onClose} disabled={busy || uncertain}>Đóng</Button>
      <Button onClick={previewReplacement} loading={busy} disabled={uncertain}>Xem trước</Button>
      {preview?.previewToken && <Button type="primary" onClick={confirmReplacement} loading={busy} disabled={uncertain}>Xác nhận thay ca</Button>}
    </Space>}
  >
    {error && <Alert className="staff-schedule-feedback" type={uncertain ? 'warning' : 'error'} showIcon title={uncertain ? 'Kết quả chưa xác định' : 'Không thể thay ca'} description={error} />}
    <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
      <Descriptions.Item label="Nhân viên hiện tại">{assignment.employeeName || 'Nhân viên'}</Descriptions.Item>
      <Descriptions.Item label="Ngày">{formatDate(assignment.workDate)}</Descriptions.Item>
      <Descriptions.Item label="Ca">{assignment.shiftName || 'Ca làm việc'}</Descriptions.Item>
      <Descriptions.Item label="Thời gian">{formatTime(assignment.startTime)}–{formatTime(assignment.endTime)}</Descriptions.Item>
    </Descriptions>
    <Form layout="vertical" disabled={busy || uncertain} className="staff-contract-stack">
      <Form.Item label="Nhân viên thay thế" required>
        <Select
          showSearch
          optionFilterProp="label"
          value={replacementEmployeeId}
          onChange={(value) => { setReplacementEmployeeId(value); invalidatePreview() }}
          options={candidates.map((employee) => ({ value: employee.employeeId, label: employee.name }))}
          placeholder="Tìm nhân viên đang làm việc"
          notFoundContent="Không có nhân viên đủ điều kiện."
        />
      </Form.Item>
      <Form.Item label="Lý do" required>
        <Input.TextArea
          rows={3}
          maxLength={1000}
          showCount
          value={reason}
          onChange={(event) => { setReason(event.target.value); invalidatePreview() }}
          placeholder="Nhập lý do thay ca"
        />
      </Form.Item>
    </Form>
    {preview && <section className="staff-contract-stack" aria-live="polite">
      {preview.status === 'ready'
        ? <Alert type="info" showIcon title="Đã kiểm tra lịch. Chưa lưu thay đổi." description={preview.reusesExistingAssignment
          ? 'Nhân viên thay thế đã có đúng ca này. Hệ thống sẽ giữ lịch hiện có và không tạo bản ghi trùng.'
          : 'Hệ thống sẽ hủy lịch của nhân viên hiện tại và tạo một lịch thay thế.'} />
        : <Alert type="warning" showIcon title="Không thể thay ca này" description={preview.message || 'Lịch có xung đột. Hãy chọn nhân viên khác hoặc kiểm tra lịch.'} />}
      <Descriptions bordered size="small" column={1}>
        <Descriptions.Item label="Nhân viên nhận ca">{preview.replacementEmployeeName || 'Chưa đủ điều kiện'}</Descriptions.Item>
        <Descriptions.Item label="Ca trực được chuyển">{preview.duties.length ? `${preview.duties.length} ca trực` : 'Không có ca trực cần chuyển'}</Descriptions.Item>
        {preview.reusesExistingAssignment && <Descriptions.Item label="Lịch thay thế"><Tag color="blue">Dùng lịch đã có</Tag></Descriptions.Item>}
      </Descriptions>
      {preview.duties.length > 0 && <>
        <Typography.Title level={5}>Ca trực được chuyển</Typography.Title>
        <ul>{preview.duties.map((duty) => <li key={duty.id}>{formatDate(duty.workDate)} · {duty.status}</li>)}</ul>
      </>}
    </section>}
  </Modal>
}
