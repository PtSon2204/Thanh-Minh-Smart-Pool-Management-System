import { useEffect, useState } from 'react'
import { Alert, Button, Card, Drawer, Image, Spin, Table, Tag, Typography, message } from 'antd'
import { CheckCircleOutlined, CloudUploadOutlined, EyeOutlined, FileImageOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { Link } from 'react-router-dom'
import { refreshSession } from '../../services/apiClient'
import { useAuthStore } from '../../features/auth/store/authStore'
import {
  useCoachApplicationDetail, useCoachCertificate, useMyCoachApplication,
  useMyCoachApplicationRequests, useSubmitCoachApplication,
} from '../../features/coachApplications/hooks/useCoachApplications'
import {
  coachApplicationStatus, formatCoachApplicationDate, getCoachApplicationError,
} from '../../features/coachApplications/components/coachApplicationView'
import '../../features/coachApplications/styles/coachApplications.css'

const { Title, Text } = Typography
const allowedTypes = ['image/jpeg', 'image/png']
const maxBytes = 5 * 1024 * 1024

export default function CoachApplicationPage() {
  const [messageApi, contextHolder] = message.useMessage()
  const [file, setFile] = useState(null)
  const [preview, setPreview] = useState(null)
  const [refreshing, setRefreshing] = useState(false)
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [selectedId, setSelectedId] = useState(null)
  const session = useAuthStore((state) => state.session)
  const roles = (session?.roles?.length ? session.roles : [session?.role]).filter(Boolean)
  const isCoach = roles.some((role) => role.toUpperCase() === 'COACH')
  const applicationQuery = useMyCoachApplication()
  const application = applicationQuery.data
  const requestsQuery = useMyCoachApplicationRequests({ pageIndex, pageSize })
  const detailQuery = useCoachApplicationDetail(selectedId)
  const certificateQuery = useCoachCertificate(selectedId)
  const submit = useSubmitCoachApplication()
  const state = coachApplicationStatus[application?.status]

  useEffect(() => {
    return () => { if (preview) URL.revokeObjectURL(preview) }
  }, [preview])

  function pickFile(event) {
    const selected = event.target.files?.[0]
    event.target.value = ''
    if (!selected) return
    if (!allowedTypes.includes(selected.type) || selected.size > maxBytes || selected.size === 0) {
      messageApi.error('Chọn ảnh JPG hoặc PNG, dung lượng không quá 5 MB.')
      return
    }
    setFile(selected)
    setPreview(URL.createObjectURL(selected))
  }

  async function sendApplication() {
    if (!file) return
    try {
      await submit.mutateAsync(file)
      setFile(null)
      setPreview(null)
      setPageIndex(1)
      messageApi.success('Đã gửi chứng chỉ. Admin sẽ xem xét yêu cầu của bạn.')
    } catch (error) {
      messageApi.error(getCoachApplicationError(error))
    }
  }

  async function updateAccess() {
    setRefreshing(true)
    try {
      await refreshSession()
      messageApi.success('Quyền truy cập đã được cập nhật.')
    } catch {
      messageApi.error('Không thể cập nhật phiên đăng nhập. Vui lòng đăng nhập lại.')
    } finally {
      setRefreshing(false)
    }
  }

  return <main className="coach-app-page">
    {contextHolder}
    <div className="coach-app-shell">
      <header className="coach-app-hero">
        <div>
          <span className="coach-app-eyebrow"><SafetyCertificateOutlined /> TRỞ THÀNH HUẤN LUYỆN VIÊN</span>
          <Title level={1}>Chia sẻ kỹ năng bơi của bạn</Title>
          <p>Gửi ảnh chứng chỉ dạy bơi để Admin xác minh. Khi được duyệt, bạn có thể tham gia quản lý lớp và lịch dạy.</p>
        </div>
        <div className="coach-app-hero-icon"><SafetyCertificateOutlined /></div>
      </header>

      {applicationQuery.isLoading ? <div className="coach-app-loading"><Spin size="large" /></div> :
        applicationQuery.isError ? <Alert showIcon type="error" message="Không thể tải trạng thái yêu cầu"
          action={<Button onClick={() => applicationQuery.refetch()}>Thử lại</Button>} /> : <>
          <div className="coach-app-grid">
            <Card className="coach-app-card" title="Yêu cầu gần nhất">
              {application ? <>
                <Tag color={state?.color} className="coach-app-status">{state?.label || application.status}</Tag>
                <div className="coach-app-facts">
                  <span>Ngày gửi</span><strong>{formatCoachApplicationDate(application.submittedAt)}</strong>
                  {application.reviewedAt && <><span>Ngày xét duyệt</span><strong>{formatCoachApplicationDate(application.reviewedAt)}</strong></>}
                  {application.revokedAt && <><span>Ngày thu hồi</span><strong>{formatCoachApplicationDate(application.revokedAt)}</strong></>}
                </div>
                {application.status === 'Rejected' && <Alert showIcon type="error" message="Yêu cầu chưa được chấp nhận"
                  description={application.reviewNote || 'Bạn có thể gửi lại chứng chỉ mới.'} />}
                {application.status === 'Pending' && <Alert showIcon type="info" message="Yêu cầu đang được xem xét"
                  description="Bạn sẽ thấy kết quả ngay tại trang này sau khi Admin xét duyệt." />}
                {application.status === 'Approved' && <div className="coach-app-approved">
                  <CheckCircleOutlined /><div><strong>Đã được cấp quyền Coach</strong><p>Bạn vẫn có thể sử dụng các chức năng Customer.</p></div>
                </div>}
                {application.status === 'Approved' && application.reviewNote &&
                  <Alert showIcon type="success" message="Ghi chú duyệt" description={application.reviewNote} style={{ marginBottom: 16 }} />}
                {application.status === 'Revoked' && <Alert showIcon type="warning"
                  message="Quyền Coach đã được thu hồi" description={application.revokeReason} style={{ marginBottom: 16 }} />}
                {application.status === 'Approved' && !isCoach && <Button type="primary" loading={refreshing}
                  onClick={updateAccess}>Cập nhật quyền trong phiên đăng nhập</Button>}
                {application.status === 'Revoked' && isCoach && <Button loading={refreshing}
                  onClick={updateAccess}>Cập nhật quyền trong phiên đăng nhập</Button>}
                {application.status === 'Approved' && isCoach && <Link className="coach-app-primary-link" to="/coach/classes">Quản lý lớp của tôi</Link>}
              </> : <div className="coach-app-empty"><FileImageOutlined /><h2>Chưa có yêu cầu</h2>
                <p>Chọn ảnh chứng chỉ để bắt đầu đăng ký trở thành Coach.</p></div>}
            </Card>

            <Card className="coach-app-card" title="Quy trình xét duyệt">
              <ol className="coach-app-steps">
                <li>Gửi ảnh chứng chỉ dạy bơi rõ ràng.</li>
                <li>Admin xem ảnh và xét duyệt yêu cầu.</li>
                <li>Xem kết quả, lý do và ảnh của từng lần nộp trong danh sách bên dưới.</li>
              </ol>
              <p className="coach-app-muted">Chỉ bạn và Admin được xem ảnh chứng chỉ.</p>
            </Card>
          </div>

          <Card className="coach-app-card coach-app-history" title="Lịch sử yêu cầu của tôi">
            {requestsQuery.isError && <Alert type="error" showIcon message="Không thể tải lịch sử yêu cầu"
              action={<Button onClick={() => requestsQuery.refetch()}>Thử lại</Button>} style={{ marginBottom: 16 }} />}
            <Table rowKey="id" loading={requestsQuery.isLoading || requestsQuery.isFetching}
              dataSource={requestsQuery.data?.items || []} scroll={{ x: 600 }}
              columns={[
                { title: 'Ngày gửi', dataIndex: 'submittedAt', key: 'submittedAt', render: formatCoachApplicationDate },
                { title: 'Trạng thái', dataIndex: 'status', key: 'status', render: (value) =>
                  <Tag color={coachApplicationStatus[value]?.color}>{coachApplicationStatus[value]?.label || value}</Tag> },
                { title: 'Ngày xét duyệt', dataIndex: 'reviewedAt', key: 'reviewedAt', render: formatCoachApplicationDate },
                { title: 'Thao tác', key: 'action', render: (_, record) =>
                  <Button icon={<EyeOutlined />} onClick={() => setSelectedId(record.id)}>Xem chi tiết</Button> },
              ]}
              pagination={{ current: pageIndex, pageSize, total: requestsQuery.data?.totalCount || 0,
                showSizeChanger: true, pageSizeOptions: ['10', '20', '50'], onChange: (page, size) => {
                  setPageIndex(size === pageSize ? page : 1); setPageSize(size)
                } }} />
          </Card>

          {(!application || application.status === 'Rejected' || application.status === 'Revoked') &&
            (!isCoach || application?.status === 'Revoked') &&
            <Card className="coach-app-card coach-app-submit" title={application ? 'Gửi lại chứng chỉ' : 'Nộp chứng chỉ'}>
            <p>Ảnh phải rõ nội dung chứng chỉ. Hỗ trợ JPG và PNG, tối đa 5 MB.</p>
            <label className="coach-app-upload">
              <input type="file" accept="image/jpeg,image/png" onChange={pickFile} aria-label="Chọn ảnh chứng chỉ" />
              <CloudUploadOutlined />
              <strong>{file ? file.name : 'Chọn ảnh chứng chỉ'}</strong>
              <span>Nhấn để chọn ảnh từ thiết bị</span>
            </label>
            {preview && <Image className="coach-app-preview" src={preview} alt="Xem trước ảnh chứng chỉ" />}
            <div className="coach-app-submit-actions">
              <Text type="secondary">Chỉ bạn và Admin được xem ảnh sau khi gửi.</Text>
              <Button type="primary" disabled={!file} loading={submit.isPending} onClick={sendApplication}>Gửi yêu cầu</Button>
            </div>
          </Card>}
        </>}
      <Drawer title="Chi tiết yêu cầu của tôi" open={Boolean(selectedId)} width={560}
        onClose={() => setSelectedId(null)}>
        {detailQuery.isLoading ? <div className="coach-app-loading"><Spin /></div> :
          detailQuery.isError ? <Alert type="error" showIcon message="Không thể tải chi tiết yêu cầu"
            action={<Button onClick={() => detailQuery.refetch()}>Thử lại</Button>} /> : detailQuery.data ? <>
            <Tag color={coachApplicationStatus[detailQuery.data.status]?.color} className="coach-app-status">
              {coachApplicationStatus[detailQuery.data.status]?.label || detailQuery.data.status}
            </Tag>
            <div className="coach-app-facts">
              <span>Ngày gửi</span><strong>{formatCoachApplicationDate(detailQuery.data.submittedAt)}</strong>
              <span>Ngày xét duyệt</span><strong>{formatCoachApplicationDate(detailQuery.data.reviewedAt)}</strong>
              {detailQuery.data.revokedAt && <><span>Ngày thu hồi</span><strong>{formatCoachApplicationDate(detailQuery.data.revokedAt)}</strong></>}
            </div>
            {detailQuery.data.status !== 'Pending' && <Alert showIcon
              type={detailQuery.data.status === 'Rejected' ? 'error' : 'success'}
              message={detailQuery.data.status === 'Rejected' ? 'Lý do từ chối' : 'Kết quả duyệt'}
              description={detailQuery.data.reviewNote || (detailQuery.data.status !== 'Rejected'
                ? 'Yêu cầu đã được duyệt. Admin không để lại ghi chú.' : 'Không có ghi chú.')}
              style={{ marginBottom: 20 }} />}
            {detailQuery.data.status === 'Revoked' && <Alert showIcon type="warning"
              message="Lý do thu hồi quyền Coach" description={detailQuery.data.revokeReason}
              style={{ marginBottom: 20 }} />}
            <h3>Ảnh chứng chỉ đã nộp</h3>
            {certificateQuery.isLoading && <Spin />}
            {certificateQuery.isError && <Alert type="error" showIcon message="Không thể xem ảnh chứng chỉ"
              action={<Button onClick={() => certificateQuery.refetch()}>Thử lại</Button>} />}
            {certificateQuery.data?.url && <Image className="coach-app-certificate" src={certificateQuery.data.url}
              alt="Ảnh chứng chỉ đã nộp" />}
          </> : <Alert type="warning" message="Không tìm thấy yêu cầu." />}
      </Drawer>
    </div>
  </main>
}
