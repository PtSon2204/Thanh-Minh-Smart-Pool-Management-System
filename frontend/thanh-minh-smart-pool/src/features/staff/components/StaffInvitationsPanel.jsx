import { useCallback, useEffect, useRef, useState } from 'react'
import { Alert, Button, Checkbox, DatePicker, Drawer, Empty, Form, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd'
import { useQueryClient } from '@tanstack/react-query'
import { onboardingService } from '../services/onboardingService'
import { formatOnboardingDate, invitationRoleOptions, normalizePhone, onboardingError, phonePattern, stateLabels, vietnamToday } from '../onboarding/onboardingUi'
import OnboardingRevisionDetails from './OnboardingRevisionDetails'
import OnboardingDocuments from './OnboardingDocuments'
import '../styles/staffOnboarding.css'

export default function StaffInvitationsPanel({ createOpen, onCloseCreate, employee, onCloseEmployee }) {
  const [form] = Form.useForm()
  const cache = useQueryClient()
  const [items, setItems] = useState([])
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [createError, setCreateError] = useState('')
  const [workspace, setWorkspace] = useState(null)
  const [selectedEmployee, setSelectedEmployee] = useState(employee)
  const [busy, setBusy] = useState(false)
  const [uncertain, setUncertain] = useState(false)
  const [decision, setDecision] = useState(null)
  const [reason, setReason] = useState('')
  const [confirmed, setConfirmed] = useState(false)
  const guard = useRef(false)
  if (employee !== selectedEmployee) {
    setSelectedEmployee(employee)
    setWorkspace(null)
  }
  const load = useCallback(() => onboardingService.list(page)
    .then((data) => { setItems(data); setError('') })
    .catch((failure) => { setError([401, 403].includes(failure.response?.status)
      ? 'Bạn không có quyền xem danh sách lời mời. Hãy kiểm tra quyền quản lý nhân viên.'
      : onboardingError(failure)) })
    .finally(() => { setLoading(false) }), [page])
  useEffect(() => { load() }, [load])
  useEffect(() => {
    if (!employee) return
    let active = true
    onboardingService.employeeDetail(employee.userId).then((data) => { if (active) setWorkspace(data) })
      .catch((failure) => { if (active) setError(onboardingError(failure)) })
    return () => { active = false }
  }, [employee])

  const mutate = async (operation, after, showError = setError) => {
    if (guard.current || uncertain) return
    guard.current = true
    setBusy(true)
    showError('')
    try {
      const data = await operation()
      setWorkspace(data)
      setDecision(null)
      after?.()
      await load()
      cache.invalidateQueries({ queryKey: ['staffs'] })
    } catch (failure) {
      showError(onboardingError(failure))
      if (!failure.response) setUncertain(true)
    } finally { guard.current = false; setBusy(false) }
  }
  const open = async (id) => {
    if (busy) return
    setLoading(true)
    try { setWorkspace(await onboardingService.detail(id)); setUncertain(false); setError('') }
    catch (failure) { setError(onboardingError(failure)) }
    finally { setLoading(false) }
  }
  const choose = (action, invitation) => {
    setReason('')
    setConfirmed(false)
    setDecision({ action, invitation, revision: workspace?.revision, correction: workspace?.correctionRequest })
  }
  const decide = () => {
    const { action, invitation, revision, correction } = decision
    const data = { reason: reason.trim(), expectedVersion: revision?.version, reviewConfirmed: confirmed }
    if (invitation) return mutate(() => onboardingService.invitationAction(invitation.id, action, { expectedVersion: invitation.version }))
    if (action === 'open-correction') return mutate(() => onboardingService.openCorrection(employee.userId, { reason: reason.trim(), expectedVersion: correction?.version }))
    return mutate(() => onboardingService.review(revision.id, action, data))
  }
  const needsReason = ['request-changes', 'reject', 'open-correction'].includes(decision?.action)
  const actions = { approve: 'Duyệt đúng phiên bản này', 'request-changes': 'Yêu cầu bổ sung', reject: 'Từ chối hồ sơ', resend: 'Gửi lại và thay liên kết', cancel: 'Hủy lời mời', 'open-correction': 'Mở bản chỉnh sửa' }
  const columns = [
    { title: 'Email / họ tên', key: 'identity', render: (_, row) => <><Typography.Text strong>{row.fullName || 'Chưa nhập họ tên'}</Typography.Text><div>{row.invitation.email}</div></> },
    { title: 'Vai trò / vào làm', key: 'role', render: (_, row) => <>{invitationRoleOptions.find((option) => option.value === row.invitation.targetRole)?.label}<div>{formatOnboardingDate(row.invitation.joinDate)}</div></> },
    { title: 'Trạng thái', key: 'state', render: (_, row) => {
      const status = row.reviewStatus === 'REJECTED' ? 'REJECTED'
        : row.invitation.status === 'CANCELLED' ? 'CANCELLED'
          : row.invitation.status === 'COMPLETED' ? 'APPROVED'
            : row.reviewStatus || row.invitation.status
      return <div className="staff-invitation-status"><Tag>{stateLabels[status] || status}</Tag><Typography.Text type="secondary">Email: {stateLabels[row.invitation.deliveryStatus] || row.invitation.deliveryStatus}</Typography.Text></div>
    } },
    { title: 'Thao tác', key: 'actions', render: (_, row) => <Space wrap><Button disabled={busy} onClick={() => open(row.invitation.id)}>Xem hồ sơ</Button>{['PENDING', 'EXPIRED'].includes(row.invitation.status) && <>{['DRAFT', 'CHANGES_REQUESTED'].includes(row.reviewStatus) && <Button disabled={busy || uncertain} onClick={() => choose('resend', row.invitation)}>Gửi lại</Button>}<Button danger disabled={busy || uncertain} onClick={() => choose('cancel', row.invitation)}>Hủy</Button></>}</Space> },
  ]
  return <section className="staff-invitations-panel" aria-label="Lời mời và duyệt hồ sơ">
    <div className="onboarding-panel-header"><Typography.Title level={3}>Lời mời và hồ sơ chờ duyệt</Typography.Title><Button disabled={loading || busy} onClick={async () => { setLoading(true); await load(); if (workspace?.invitation) await open(workspace.invitation.id); setUncertain(false) }}>Tải lại trạng thái</Button></div>
    {error && <Alert type="error" showIcon title="Không thể hoàn tất thao tác" description={error} />}
    {uncertain && <Alert type="warning" showIcon title="Kết quả chưa xác định" description="Không gửi lại ngay. Tải lại trạng thái và kiểm tra lời mời trước khi tiếp tục." />}
    <Table rowKey={(row) => row.invitation.id} dataSource={items} columns={columns} loading={loading} pagination={false} scroll={{ x: 780 }} locale={{ emptyText: <Empty description="Chưa có lời mời." /> }} />
    <Space className="onboarding-actions"><Button disabled={page === 1 || loading} onClick={() => { setLoading(true); setPage(page - 1) }}>Trang trước</Button><span>Trang {page}</span><Button disabled={items.length < 20 || loading} onClick={() => { setLoading(true); setPage(page + 1) }}>Trang sau</Button></Space>
    <Drawer title="Mời nhân viên mới" open={createOpen} onClose={() => { if (!busy) { setCreateError(''); onCloseCreate() } }} width={520} destroyOnHidden>
      <div className="staff-invitation-content">
      <Alert type="info" title="Chỉ dành cho email chưa có tài khoản" description="Không chuyển khách hàng thành nhân viên. Lời mời chưa tạo tài khoản hoặc quyền làm việc." />
      {createError && <Alert type="error" title={createError} />}
      <Form name="staff-invitation" form={form} layout="vertical" disabled={busy || uncertain} onFinish={(values) => mutate(() => onboardingService.create({ ...values, fullName: values.fullName.trim(), phone: normalizePhone(values.phone), email: values.email.trim().toLowerCase(), joinDate: values.joinDate.format('YYYY-MM-DD') }), () => { setWorkspace(null); form.resetFields(); onCloseCreate() }, setCreateError)}>
        <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email', message: 'Nhập email hợp lệ.' }, { max: 150 }]}><Input autoComplete="email" /></Form.Item>
        <Form.Item name="targetRole" label="Vai trò" rules={[{ required: true, message: 'Chọn vai trò.' }]}><Select options={invitationRoleOptions} /></Form.Item>
        <Form.Item name="joinDate" label="Ngày vào làm" rules={[{ required: true, message: 'Chọn ngày vào làm.' }, { validator: (_, value) => !value || value.format('YYYY-MM-DD') >= vietnamToday() ? Promise.resolve() : Promise.reject(new Error('Ngày vào làm phải từ hôm nay trở đi.')) }]}><DatePicker format="DD/MM/YYYY" disabledDate={(date) => date.format('YYYY-MM-DD') < vietnamToday()} className="service-full-width" /></Form.Item>
        <Form.Item name="fullName" label="Họ và tên" rules={[{ required: true, whitespace: true, message: 'Nhập họ và tên.' }, { max: 255 }]}><Input maxLength={255} autoComplete="name" /></Form.Item>
        <Form.Item name="phone" label="Điện thoại" rules={[{ required: true, whitespace: true, message: 'Nhập số điện thoại.' }, { pattern: phonePattern, message: 'Nhập số di động Việt Nam, bắt đầu bằng 0 hoặc +84.' }]}><Input inputMode="tel" autoComplete="tel" /></Form.Item>
        <Button type="primary" htmlType="submit" loading={busy}>Tạo và gửi lời mời</Button>
      </Form>
      </div>
    </Drawer>
    <Drawer title={employee ? `Hồ sơ pháp lý: ${employee.fullName || employee.email}` : 'Duyệt hồ sơ lời mời'} open={Boolean(workspace) || Boolean(employee)} onClose={() => { if (!busy) { setWorkspace(null); onCloseEmployee?.() } }} width={850} destroyOnHidden>
      <div className="staff-review-content">
      {error && <Alert type="error" title={error} />}
      {workspace && <>
        {workspace.invitation?.deliveryStatus === 'FAILED' && <Alert type="warning" title="Email chưa gửi thành công" description="Chỉ gửi lại khi bạn chủ động chọn. Liên kết cũ sẽ bị thay thế." />}
        <OnboardingRevisionDetails revision={workspace.revision || workspace.approvedRevision} />
        <OnboardingDocuments documents={(workspace.revision || workspace.approvedRevision)?.documents} onRead={onboardingService.readReviewDocument} />
        {workspace.revision?.state === 'SUBMITTED' && <Space wrap className="onboarding-actions"><Button type="primary" disabled={busy || uncertain} onClick={() => choose('approve')}>Duyệt hồ sơ</Button><Button disabled={busy || uncertain} onClick={() => choose('request-changes')}>Yêu cầu bổ sung</Button><Button danger disabled={busy || uncertain} onClick={() => choose('reject')}>Từ chối</Button></Space>}
        {workspace.correctionRequest && <Alert type="info" title={stateLabels[workspace.correctionRequest.state] || workspace.correctionRequest.state} description={workspace.correctionRequest.reason} />}
        {employee && workspace.correctionRequest?.state === 'REQUESTED' && <Button disabled={busy || uncertain} onClick={() => choose('open-correction')}>Mở chỉnh sửa theo yêu cầu</Button>}
        {workspace.approvedRevision && workspace.revision?.id !== workspace.approvedRevision.id && <><Typography.Title level={4}>Bản đã duyệt đang có hiệu lực</Typography.Title><OnboardingRevisionDetails revision={workspace.approvedRevision} /></>}
      </>}
      </div>
    </Drawer>
    <Modal title={actions[decision?.action]} open={Boolean(decision)} onCancel={() => !busy && setDecision(null)} footer={<Space><Button disabled={busy} onClick={() => setDecision(null)}>Quay lại</Button><Button type="primary" danger={['reject', 'cancel'].includes(decision?.action)} loading={busy} disabled={uncertain || (needsReason && !reason.trim()) || (decision?.action === 'approve' && !confirmed)} onClick={decide}>{actions[decision?.action]}</Button></Space>} destroyOnHidden>
      <div className="staff-review-confirmation">
      {error && <Alert type="error" title={error} />}
      {needsReason && <Input.TextArea aria-label="Lý do quyết định" rows={4} maxLength={1000} showCount value={reason} onChange={(event) => setReason(event.target.value)} />}
      {decision?.action === 'approve' && <Checkbox checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)}>Tôi đã kiểm tra đúng phiên bản này. Đây là duyệt thủ công, không phải xác minh của cơ quan nhà nước. Ảnh không khả dụng không được coi là đã đối chiếu.</Checkbox>}
      {['approve', 'reject', 'cancel'].includes(decision?.action) && <Typography.Paragraph>Quyết định sẽ thu hồi truy cập ảnh CCCD và yêu cầu xóa. Lỗi nhà cung cấp có thể làm chậm việc xóa thực tế.</Typography.Paragraph>}
      {decision?.action === 'resend' && <Typography.Paragraph>Liên kết cũ sẽ ngừng hoạt động. Hệ thống không tự gửi lại khi kết quả chưa rõ.</Typography.Paragraph>}
      </div>
    </Modal>
  </section>
}
