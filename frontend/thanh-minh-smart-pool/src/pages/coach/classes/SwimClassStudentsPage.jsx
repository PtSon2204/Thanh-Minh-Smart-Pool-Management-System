import { Alert, Button, ConfigProvider, Spin } from 'antd'
import { ArrowLeftOutlined, ReloadOutlined, TeamOutlined } from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuthStore } from '../../../features/auth/store/authStore'
import { useSwimClassDetail } from '../../../features/swimClasses/hooks/useSwimClasses'
import SwimClassStudents from '../../../features/swimClasses/components/SwimClassStudents'
import '../../../features/swimClasses/styles/swimClassDetail.css'

export default function SwimClassStudentsPage() {
  const { classId } = useParams()
  const navigate = useNavigate()
  const userId = useAuthStore(state => state.session?.id)
  const detail = useSwimClassDetail(classId, userId, 'coach')
  const isOwner = detail.data?.createdByUserId === userId

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <main className="swim-class-detail-page swim-class-detail-page--coach">
      <Button className="swim-class-detail-back" type="text" icon={<ArrowLeftOutlined />}
        onClick={() => navigate(`/coach/classes/${classId}`)}>Về chi tiết lớp</Button>
      {detail.isPending && <div className="swim-class-detail-loading"><Spin size="large" /></div>}
      {detail.isError && <div className="swim-class-detail-panel"><Alert type="error" showIcon
        title="Không thể tải thông tin lớp học"
        action={<Button icon={<ReloadOutlined />} onClick={() => detail.refetch()}>Thử lại</Button>} /></div>}
      {detail.data && !detail.isError && !isOwner && <Alert type="warning" showIcon
        title="Bạn không có quyền xem danh sách học viên của lớp này." />}
      {detail.data && !detail.isError && isOwner && <>
        <header className="swim-class-detail-header">
          <div className="swim-class-detail-heading"><span className="swim-class-detail-icon"><TeamOutlined /></span>
            <div><small>QUẢN LÝ LỚP DẠY BƠI</small><h1>Học viên đăng ký · {detail.data.name}</h1></div>
          </div>
        </header>
        <SwimClassStudents classId={classId} />
      </>}
    </main>
  </ConfigProvider>
}
