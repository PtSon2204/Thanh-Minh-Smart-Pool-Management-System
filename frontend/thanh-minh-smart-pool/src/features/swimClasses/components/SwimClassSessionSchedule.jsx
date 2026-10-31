import { useState } from 'react'
import { CalendarOutlined, EditOutlined, LeftOutlined, RightOutlined, UserOutlined } from '@ant-design/icons'
import { Button, Empty, Tag } from 'antd'
import { recommendedWeekStart, sessionTimeRange, shiftWeek, weekDays, weekRangeLabel, weekStartFor } from '../utils/classTimetable'

const sessionStatus = {
  Scheduled: { label: 'Đã lên lịch', color: 'processing' },
  Cancelled: { label: 'Đã hủy', color: 'error' },
  Completed: { label: 'Đã hoàn tất', color: 'success' },
}

export default function SwimClassSessionSchedule({ sessions, now, onEditSession }) {
  const [visibleWeek, setVisibleWeek] = useState(() => recommendedWeekStart(sessions, now))
  const nearestWeek = recommendedWeekStart(sessions, now)
  const currentWeek = weekStartFor(now)
  const days = weekDays(visibleWeek, sessions, now)
  const visibleCount = days.reduce((total, day) => total + day.sessions.length, 0)

  return <section className="swim-class-detail-panel" aria-label="Thời khóa biểu lớp học">
    <div className="swim-class-detail-section-title"><CalendarOutlined /><h2>Thời khóa biểu</h2><span>{sessions.length} buổi</span></div>
    {sessions.length === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Lớp chưa có buổi học." /> : <>
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
                    {onEditSession && session.status === 'Scheduled' && Date.parse(session.startsAt) > now &&
                      <Button className="swim-class-timetable-edit" size="small" icon={<EditOutlined />}
                        onClick={() => onEditSession(session)} aria-label={`Sửa buổi ${sessionTimeRange(session)} ngày ${day.dateLabel}`}>Sửa buổi</Button>}
                    {session.status === 'Cancelled' && session.cancelReason && <p className="swim-class-timetable-cancel-reason">Lý do hủy: {session.cancelReason}</p>}
                  </article>
                })}
            </div>
          </section>)}
        </div>
      </div>
      {visibleCount === 0 && <p className="swim-class-timetable-no-week">Tuần này chưa có buổi học. Chọn tuần khác để xem lịch.</p>}
      <p className="swim-class-detail-timezone">Giờ học hiển thị theo giờ Việt Nam (UTC+7). Trên màn hình nhỏ, vuốt ngang để xem đủ bảy ngày.</p>
    </>}
  </section>
}
