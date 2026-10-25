import { useEffect, useState } from 'react'
import { Alert, Button, ConfigProvider, Spin } from 'antd'
import { ArrowLeftOutlined, BookOutlined, ReloadOutlined } from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useSwimClassDetail } from '../hooks/useSwimClasses'
import SwimClassStatusTag from './SwimClassStatusTag'
import SwimClassDetailSummary from './SwimClassDetailSummary'
import SwimClassSessionSchedule from './SwimClassSessionSchedule'
import SwimClassDraftActions from './SwimClassDraftActions'
import '../styles/swimClassManagement.css'
import '../styles/swimClassDetail.css'

export default function SwimClassDetailView() {
  const { classId } = useParams()
  const navigate = useNavigate()
  const session = useAuthStore(state => state.session)
  const role = session?.role?.toUpperCase()
  const backPath = role === 'CUSTOMER' ? '/lop-hoc' : '/admin/classes'
  const detail = useSwimClassDetail(classId, session?.id)
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 60_000)
    return () => clearInterval(timer)
  }, [])

  const swimClass = detail.data
  const notFound = detail.error?.response?.status === 404
  const forbidden = detail.error?.response?.status === 403

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <div className={`swim-class-detail-page${role === 'CUSTOMER' ? ' swim-class-detail-page--customer' : ''}`}>
      <Button className="swim-class-detail-back" type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate(backPath)}>Danh sách lớp</Button>

      {detail.isPending && <div className="swim-class-detail-loading"><Spin size="large" tip="Đang tải lớp học..." /></div>}

      {detail.isError && <div className="swim-class-detail-panel"><Alert type={notFound ? 'warning' : 'error'} showIcon
        title={notFound ? 'Lớp không tồn tại hoặc không còn khả dụng với tài khoản của bạn.'
          : forbidden ? 'Bạn không có quyền xem lớp này.' : 'Không thể tải thông tin lớp học.'}
        description={notFound ? 'Lớp có thể đã ngừng hoạt động hoặc quyền xem của bạn đã thay đổi.' : undefined}
        action={!notFound && !forbidden ? <Button icon={<ReloadOutlined />} onClick={() => detail.refetch()}>Thử lại</Button> : undefined} /></div>}

      {swimClass && !detail.isError && <>
        <header className="swim-class-detail-header">
          <div className="swim-class-detail-heading"><span className="swim-class-detail-icon"><BookOutlined /></span><div><small>CHI TIẾT LỚP DẠY BƠI</small><h1>{swimClass.name}</h1></div></div>
          <SwimClassStatusTag status={swimClass.status} />
        </header>
        <SwimClassDetailSummary swimClass={swimClass} now={now} />
        <SwimClassSessionSchedule sessions={swimClass.sessions || []} now={now} />
        {role === 'ADMIN' && swimClass.status === 'Draft' && <SwimClassDraftActions swimClass={swimClass} now={now} />}
      </>}
    </div>
  </ConfigProvider>
}
