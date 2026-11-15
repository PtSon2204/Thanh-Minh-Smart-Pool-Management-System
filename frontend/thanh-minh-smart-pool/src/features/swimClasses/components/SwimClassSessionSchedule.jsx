import { useState } from 'react'
import { CalendarOutlined, CameraOutlined, EditOutlined, LeftOutlined, RightOutlined, StopOutlined, UnorderedListOutlined, UserOutlined } from '@ant-design/icons'
import { Button, Empty, Tag } from 'antd'
import { recommendedWeekStart, sessionTimeRange, shiftWeek, weekDays, weekRangeLabel, weekStartFor } from '../utils/classTimetable'
import { formatVietnamTime } from '../utils/classSchedule'

const sessionStatus = {
  Scheduled: { label: 'Đã lên lịch', color: 'processing' },
  Cancelled: { label: 'Đã hủy', color: 'error' },
  Completed: { label: 'Đã hoàn tất', color: 'success' },
}

export default function SwimClassSessionSchedule({ sessions, now, onEditSession, onCancelSession, onViewAttendance, attendanceCoachUserId }) {
  const [visibleWeek, setVisibleWeek] = useState(() => recommendedWeekStart(sessions, now))
  const [view, setView] = useState('list')
  const nearestWeek = recommendedWeekStart(sessions, now)
  const currentWeek = weekStartFor(now)
  const days = weekDays(visibleWeek, sessions, now)
  const visibleCount = days.reduce((total, day) => total + day.sessions.length, 0)
  const sortedSessions = [...sessions].sort((first, second) => Date.parse(first.startsAt) - Date.parse(second.startsAt))

  return <section className="swim-class-detail-panel" aria-label="Thời khóa biểu lớp học">
    <div className="swim-class-detail-section-title"><CalendarOutlined /><h2>Lịch học</h2><span>{sessions.length} buổi</span></div>
    {sessions.length === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Lớp chưa có buổi học." /> : <>
      <div className="swim-class-schedule-view-switch" role="group" aria-label="Kiểu hiển thị lịch học">
        <Button type={view === 'list' ? 'primary' : 'default'} icon={<UnorderedListOutlined />} onClick={() => setView('list')}>Danh sách buổi học</Button>
        <Button type={view === 'week' ? 'primary' : 'default'} icon={<CalendarOutlined />} onClick={() => setView('week')}>Theo tuần</Button>
      </div>
      {view === 'list' && <div className="swim-class-session-list">
        {sortedSessions.map((session, index) => {
          const state = sessionStatus[session.status] || { label: session.status || 'Chưa xác định', color: 'default' }
          return <article className="swim-class-session-list-row" key={session.id}>
            <span className="swim-class-session-list-number">{index + 1}</span>
            <div className="swim-class-session-list-content"><strong>{formatVietnamTime(session.startsAt)} – {formatVietnamTime(session.endsAt).slice(-5)}</strong>
              <span><UserOutlined aria-hidden="true" /> {session.coachName || session.coachUserId}</span>
              {session.status === 'Cancelled' && session.cancelReason && <span>Lý do hủy: {session.cancelReason}</span>}
            </div>
            <Tag color={state.color}>{state.label}</Tag>
            {onViewAttendance && session.coachUserId === attendanceCoachUserId &&
              <Button icon={<CameraOutlined />} onClick={() => onViewAttendance(session)}>Điểm danh</Button>}
            {onEditSession && session.status === 'Scheduled' && Date.parse(session.startsAt) > now &&
              <Button icon={<EditOutlined />} onClick={() => onEditSession(session)}>Sửa buổi</Button>}
            {onCancelSession && session.status === 'Scheduled' && Date.parse(session.startsAt) > now &&
              <Button danger icon={<StopOutlined />} onClick={() => onCancelSession(session)}>Hủy buổi</Button>}
          </article>
        })}
      </div>}
      {view === 'week' && <>
      <div className="swim-class-timetable-toolbar">
        <div className="swim-class-timetable-nav">
          <Button icon={<LeftOutlined />} aria-label="Tuần trước" onClick={() => setVisibleWeek(week => shiftWeek(week, -1))} />
          <strong aria-live="polite">{weekRangeLabel(visibleWeek)}</strong>
          <Button icon={<RightOutlined />} aria-label="Tuần sau" onClick={() => setVisibleWeek(week => shiftWeek(week, 1))} />
        </div>
        <div className="swim-class-timetable-shortcuts">
          <Button onClick={() => setVisibleWeek(currentWeek)} disabled={visibleWeek === currentWeek}>Tuần hiện tại</Button>
          <Button onClick={() => setVisibleWeek(nearestWeek)} disabled={visibleWeek === nearestWeek}>Buổi gần nhất</Button>
        </div>
      </div>
      <div className="swim-class-timetable-scroll" role="region" aria-label={`Thời khóa biểu tuần ${weekRangeLabel(visibleWeek)}`} tabIndex={0}>
        <div className="swim-class-timetable-grid">
          {days.map(day => <section key={day.dateKey} className={`swim-class-timetable-day${day.isToday ? ' is-today' : ''}`} aria-label={`${day.label}, ${day.dateLabel}`}>
            <header className="swim-class-timetable-day-header"><span>{day.label}</span><strong>{day.dateLabel}</strong></header>
            <div className="swim-class-timetable-day-body">
              {day.sessions.length === 0 ? <span className="swim-class-timetable-empty-day">Không có buổi</span>
                : day.sessions.map(session => {
                  const state = sessionStatus[session.status] || { label: session.status || 'Chưa xác định', color: 'default' }
                  return <article key={session.id} className={`swim-class-timetable-event${session.status === 'Cancelled' ? ' is-cancelled' : ''}`}>
                    <strong className="swim-class-timetable-time">{sessionTimeRange(session)}</strong>
                    <span className="swim-class-timetable-coach"><UserOutlined aria-hidden="true" /> {session.coachName || session.coachUserId}</span>
                    <Tag color={state.color}>{state.label}</Tag>
                    {onViewAttendance && session.coachUserId === attendanceCoachUserId &&
                      <Button className="swim-class-timetable-edit" size="small" icon={<CameraOutlined />}
                        onClick={() => onViewAttendance(session)}>Điểm danh</Button>}
                    {onEditSession && session.status === 'Scheduled' && Date.parse(session.startsAt) > now &&
                      <Button className="swim-class-timetable-edit" size="small" icon={<EditOutlined />}
                        onClick={() => onEditSession(session)} aria-label={`Sửa buổi ${sessionTimeRange(session)} ngày ${day.dateLabel}`}>Sửa buổi</Button>}
                    {onCancelSession && session.status === 'Scheduled' && Date.parse(session.startsAt) > now &&
                      <Button danger size="small" icon={<StopOutlined />}
                        onClick={() => onCancelSession(session)} aria-label={`Hủy buổi ${sessionTimeRange(session)} ngày ${day.dateLabel}`}>Hủy buổi</Button>}
                    {session.status === 'Cancelled' && session.cancelReason && <p className="swim-class-timetable-cancel-reason">Lý do hủy: {session.cancelReason}</p>}
                  </article>
                })}
            </div>
          </section>)}
        </div>
      </div>
      {visibleCount === 0 && <p className="swim-class-timetable-no-week">Tuần này chưa có buổi học. Chọn tuần khác để xem lịch.</p>}
      <p className="swim-class-detail-timezone">Trên màn hình nhỏ, vuốt ngang để xem đủ bảy ngày.</p>
      </>}
      <p className="swim-class-detail-timezone">Giờ học hiển thị theo giờ Việt Nam (UTC+7).</p>
    </>}
  </section>
}
