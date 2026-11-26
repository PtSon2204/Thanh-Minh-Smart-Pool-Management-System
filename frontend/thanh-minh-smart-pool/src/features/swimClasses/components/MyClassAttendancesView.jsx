import { useState } from 'react'
import { Alert, Button, Empty, Image, Pagination, Spin, Tag } from 'antd'
import { ArrowLeftOutlined, ReloadOutlined } from '@ant-design/icons'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useMyClassAttendances } from '../hooks/useClassAttendances'
import { attendanceStatus } from '../utils/classAttendance'
import { formatVietnamTime } from '../utils/classSchedule'
import '../styles/classAttendance.css'

export default function MyClassAttendancesView() {
  const { classId } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const userId = useAuthStore(state => state.session?.id)
  const [pageIndex, setPageIndex] = useState(1)
  const attendances = useMyClassAttendances(classId, userId, pageIndex)
  return <main className="class-attendance-page">
    <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate('/lop-hoc-cua-toi')}>Về lớp học của tôi</Button>
    <header className="class-attendance-header"><div><small>GÓC HỌC VIÊN</small><h1>Điểm danh · {location.state?.className || 'Lớp học của tôi'}</h1>
      <p>Xem kết quả điểm danh và ảnh do Coach ghi nhận.</p></div></header>
    <section className="class-attendance-panel" aria-label="Điểm danh theo buổi">
      <div className="class-attendance-toolbar"><h2>Buổi học</h2><Button icon={<ReloadOutlined />} onClick={() => attendances.refetch()}>Làm mới</Button></div>
      {attendances.isPending && <Spin />}
      {attendances.isError && <Alert type="error" showIcon title="Không tải được điểm danh." />}
      {!attendances.isPending && !attendances.isError && !attendances.data?.items?.length && <Empty description="Lớp chưa có buổi học." />}
      <div className="class-attendance-list">{(attendances.data?.items || []).map(item => {
        const status = item.sessionStatus === 'Cancelled' ? 'Cancelled' : !item.eligibleForAttendance ? 'NotApplicable' : item.attendance?.status || 'NotMarked'
        return <article className="class-attendance-row" key={item.sessionId}>
          <div className="class-attendance-row-main"><strong>{formatVietnamTime(item.startsAt)} – {formatVietnamTime(item.endsAt).slice(-5)}</strong>
            <span>Giờ Việt Nam (UTC+7)</span></div>
          <Tag color={attendanceStatus[status]?.color || 'default'}>{attendanceStatus[status]?.label || status}</Tag>
          {item.attendance?.imageUrl && <Image width={72} height={54} style={{ objectFit: 'cover' }} src={item.attendance.imageUrl} alt="Ảnh điểm danh của tôi" />}
        </article>
      })}</div>
      {(attendances.data?.totalCount || 0) > 20 && <Pagination current={pageIndex} pageSize={20}
        total={attendances.data.totalCount} showSizeChanger={false} onChange={setPageIndex} />}
    </section>
  </main>
}
