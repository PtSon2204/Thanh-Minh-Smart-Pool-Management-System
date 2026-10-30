import { useEffect, useState } from 'react'
import { Alert, Button, ConfigProvider, Spin } from 'antd'
import { ArrowLeftOutlined, BookOutlined, EditOutlined, ReloadOutlined } from '@ant-design/icons'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useSwimClassDetail } from '../hooks/useSwimClasses'
import SwimClassStatusTag from './SwimClassStatusTag'
import SwimClassDetailSummary from './SwimClassDetailSummary'
import SwimClassSessionSchedule from './SwimClassSessionSchedule'
import SwimClassDraftActions from './SwimClassDraftActions'
import SwimClassEditModal from './SwimClassEditModal'
import SwimClassSessionEditModal from './SwimClassSessionEditModal'
import SwimClassRegistrationActions from './SwimClassRegistrationActions'
import '../styles/swimClassManagement.css'
import '../styles/swimClassDetail.css'

export default function SwimClassDetailView() {
  const { classId } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const session = useAuthStore(state => state.session)
  const roles = (session?.roles?.length ? session.roles : [session?.role])
    .filter(Boolean).map(item => item.toUpperCase())
  const role = location.pathname.startsWith('/coach/classes') && roles.includes('COACH')
    ? 'COACH' : session?.role?.toUpperCase()
  const fromMyClasses = role === 'CUSTOMER' && location.state?.fromMyClasses === true
  const backPath = role === 'COACH' ? '/coach/classes'
    : (fromMyClasses ? '/lop-hoc-cua-toi' : '/lop-hoc')
  const detail = useSwimClassDetail(classId, session?.id, role === 'COACH' ? 'coach' : undefined)
  const [now, setNow] = useState(() => Date.now())
  const [editingClass, setEditingClass] = useState(false)
  const [editingSession, setEditingSession] = useState(null)

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 60_000)
    return () => clearInterval(timer)
  }, [])

  const swimClass = detail.data
  const isOwner = role === 'COACH' && swimClass?.createdByUserId === session?.id
  const notFound = detail.error?.response?.status === 404
  const forbidden = detail.error?.response?.status === 403

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <div className={`swim-class-detail-page${role === 'CUSTOMER' ? ' swim-class-detail-page--customer' : ' swim-class-detail-page--coach'}`}>
      <Button className="swim-class-detail-back" type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate(backPath)}>{fromMyClasses ? 'Lớp học của tôi' : 'Danh sách lớp'}</Button>

      {detail.isPending && <div className="swim-class-detail-loading"><Spin size="large" tip="Đang tải lớp học..." /></div>}

      {detail.isError && <div className="swim-class-detail-panel"><Alert type={notFound ? 'warning' : 'error'} showIcon
        title={notFound ? 'Lớp không tồn tại hoặc không còn khả dụng với tài khoản của bạn.'
          : forbidden ? 'Bạn không có quyền xem lớp này.' : 'Không thể tải thông tin lớp học.'}
        description={notFound ? 'Lớp có thể đã ngừng hoạt động hoặc quyền xem của bạn đã thay đổi.' : undefined}
        action={!notFound && !forbidden ? <Button icon={<ReloadOutlined />} onClick={() => detail.refetch()}>Thử lại</Button> : undefined} /></div>}

      {swimClass && !detail.isError && <>
        <header className="swim-class-detail-header">
          <div className="swim-class-detail-heading"><span className="swim-class-detail-icon"><BookOutlined /></span><div><small>CHI TIẾT LỚP DẠY BƠI</small><h1>{swimClass.name}</h1></div></div>
          <div className="swim-class-detail-header-actions">
            {isOwner && <Button icon={<EditOutlined />} onClick={() => setEditingClass(true)}>Chỉnh sửa</Button>}
            <SwimClassStatusTag status={swimClass.status} />
          </div>
        </header>
        <SwimClassDetailSummary swimClass={swimClass} now={now} />
        {role === 'CUSTOMER' && <SwimClassRegistrationActions key={swimClass.id} swimClass={swimClass}
          userId={session.id} now={now} onRefresh={() => detail.refetch()}
          initialPaymentOpen={location.state?.openPayment === true} />}
        <SwimClassSessionSchedule sessions={swimClass.sessions || []} now={now}
          onEditSession={isOwner && ['Draft', 'Active'].includes(swimClass.status) ? setEditingSession : undefined} />
        {isOwner && ['Draft', 'Active'].includes(swimClass.status) && <SwimClassDraftActions swimClass={swimClass} now={now} />}
        {isOwner && editingClass && <SwimClassEditModal swimClass={swimClass} open
          onClose={() => setEditingClass(false)} onReload={() => detail.refetch()} />}
        {isOwner && editingSession && <SwimClassSessionEditModal classId={swimClass.id} session={editingSession}
          onClose={() => setEditingSession(null)} onReload={() => detail.refetch()} />}
      </>}
    </div>
  </ConfigProvider>
}
