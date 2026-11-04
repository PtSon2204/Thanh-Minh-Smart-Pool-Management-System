import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Empty, Spin } from 'antd'
import dayjs from 'dayjs'
import {
  Bar, BarChart, CartesianGrid, Cell, Pie, PieChart,
  ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import ticketService from '../../../features/tickets/services/ticketService'
import { vietnamDate } from '../../../features/tickets/utils/poolAccessState'
import '../../../features/services/styles/serviceManagement.css'
import './DailySalesPage.css'

const countFormat = new Intl.NumberFormat('vi-VN')
const averageFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 })
const percentFormat = new Intl.NumberFormat('vi-VN', { style: 'percent', maximumFractionDigits: 1 })
const updatedFormat = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'medium',
})
const chartColors = [
  'var(--primary)', 'var(--primary-dark)', 'var(--secondary)',
  'var(--primary-light)', 'var(--text-muted)', 'var(--text-secondary)',
]
const formatHour = (hour) => `${String(hour).padStart(2, '0')}:00`
const formatHourRange = (hour) => `${formatHour(hour)} – ${formatHour((hour + 1) % 24)}`
const ticketTooltip = (value) => [`${countFormat.format(value)} vé`, 'Số vé']

function DailySalesMetric({ label, value, detail }) {
  return <div className="daily-sales-metric">
    <dt>{label}</dt>
    <dd>{value}</dd>
    {detail && <p>{detail}</p>}
  </div>
}

function DailySalesBreakdown({ title, items, emptyText }) {
  const total = items.reduce((sum, item) => sum + item.ticketCount, 0)

  return <section className="daily-sales-panel" aria-label={title}>
    <h2>{title}</h2>
    {total === 0 ? <div className="daily-sales-chart-empty">
      <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyText} />
    </div> : <>
      <div className="daily-sales-donut">
        <ResponsiveContainer width="100%" height="100%" minWidth={0}>
          <PieChart accessibilityLayer title={title}>
            <Pie data={items} dataKey="ticketCount" nameKey="name" cx="50%" cy="50%"
              innerRadius={64} outerRadius={88} stroke="var(--admin-surface)" isAnimationActive={false}>
              {items.map((item, index) => <Cell key={item.name} fill={chartColors[index % chartColors.length]} />)}
            </Pie>
            <Tooltip formatter={ticketTooltip} />
          </PieChart>
        </ResponsiveContainer>
        <div className="daily-sales-donut-total" aria-hidden="true">
          <strong>{countFormat.format(total)}</strong><span>vé</span>
        </div>
      </div>
      <p className="daily-sales-breakdown-total">Tổng cộng: {countFormat.format(total)} vé</p>
      <ul className="daily-sales-legend">
        {items.map((item, index) => <li key={item.name}>
          <span className="daily-sales-legend-name">
            <span className="daily-sales-legend-dot" aria-hidden="true" style={{ backgroundColor: chartColors[index % chartColors.length] }} />
            <span>{item.name}</span>
          </span>
          <strong>{countFormat.format(item.ticketCount)} vé</strong>
          <span className="daily-sales-legend-percent">{percentFormat.format(item.ticketCount / total)}</span>
        </li>)}
      </ul>
    </>}
  </section>
}

export default function DailySalesPage() {
  const [date, setDate] = useState(vietnamDate)
  const query = useQuery({
    queryKey: ['tickets', 'daily-sales', date],
    queryFn: ({ signal }) => ticketService.getDailySales(date, signal),
    retry: false,
  })
  const report = query.data
  const today = vietnamDate()
  const hourlySales = report ? Array.from({ length: 24 }, (_, hour) => ({
    hour,
    ticketCount: report.hourlySales.find((item) => item.hour === hour)?.ticketCount ?? 0,
  })) : []

  const showToday = () => {
    const currentDate = vietnamDate()
    if (date === currentDate) void query.refetch()
    else setDate(currentDate)
  }

  return <div className="daily-sales-page">
    <header className="daily-sales-header">
      <div>
        <h1>Vé bán trong ngày</h1>
        <p>Số lượng vé và đơn bán vé theo ngày. Múi giờ Việt Nam (UTC+7).</p>
      </div>
      <div className="daily-sales-controls">
        <label className="daily-sales-date" htmlFor="daily-sales-date">
          <span>Ngày bán vé</span>
          <DatePicker id="daily-sales-date" value={dayjs(date)} format="DD/MM/YYYY" allowClear={false}
            disabledDate={(value) => value.format('YYYY-MM-DD') > today}
            onChange={(value) => { if (value) setDate(value.format('YYYY-MM-DD')) }} />
        </label>
        <Button onClick={showToday} disabled={query.isFetching}>Hôm nay</Button>
        <Button icon={<ReloadOutlined />} loading={query.isFetching} onClick={() => query.refetch()}>Làm mới</Button>
      </div>
    </header>

    <div className="daily-sales-content" aria-busy={query.isFetching}>
      {query.isFetching ? <div className="daily-sales-loading" role="status">
        <Spin size="large" /><span>Đang tải số liệu bán vé…</span>
      </div> : query.isError ? <Alert type="error" showIcon title="Không thể tải số liệu bán vé"
        description="Chưa tải được số liệu của ngày đã chọn. Hãy thử lại."
        action={<Button onClick={() => query.refetch()}>Thử lại</Button>} /> : report ? <>
        <p className="daily-sales-updated" role="status">
          Cập nhật lúc {updatedFormat.format(new Date(query.dataUpdatedAt))} (UTC+7)
        </p>
        <dl className="daily-sales-metrics">
          <DailySalesMetric label="Vé đã bán" value={countFormat.format(report.totalTickets)} />
          <DailySalesMetric label="Đơn bán vé" value={countFormat.format(report.totalOrders)} />
          <DailySalesMetric label="Vé trung bình / đơn" value={averageFormat.format(report.averageTicketsPerOrder)} />
          <DailySalesMetric label={report.admissionLabel} value={countFormat.format(report.admissionCount)} />
        </dl>

        <section className="daily-sales-panel" aria-label="Vé bán theo giờ">
          <h2>Vé bán theo giờ</h2>
          {report.peakHour !== null && <p>Giờ cao điểm: {formatHourRange(report.peakHour)} · {countFormat.format(report.peakHourTickets)} vé</p>}
          <p>Số lượng vé trong từng khung giờ của ngày {dayjs(report.date).format('DD/MM/YYYY')}.</p>
          {report.totalTickets === 0 ? <div className="daily-sales-hourly daily-sales-chart-empty">
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có vé bán trong ngày đã chọn." />
          </div> : <div className="daily-sales-hourly">
            <ResponsiveContainer width="100%" height="100%" minWidth={0}>
              <BarChart data={hourlySales} accessibilityLayer title="Số vé bán theo giờ, giờ Việt Nam">
                <CartesianGrid vertical={false} stroke="var(--list-divider)" />
                <XAxis dataKey="hour" tickFormatter={(hour) => `${hour}h`} minTickGap={16}
                  tick={{ fill: 'var(--text-muted)', fontSize: 12 }} axisLine={false} tickLine={false} />
                <YAxis allowDecimals={false} tickFormatter={(value) => countFormat.format(value)}
                  tick={{ fill: 'var(--text-muted)', fontSize: 12 }} axisLine={false} tickLine={false} />
                <Tooltip labelFormatter={formatHourRange} formatter={ticketTooltip} cursor={{ fill: 'var(--bg-soft)' }} />
                <Bar dataKey="ticketCount" name="Số vé" fill="var(--primary)" isAnimationActive={false} />
              </BarChart>
            </ResponsiveContainer>
          </div>}
        </section>

        <div className="daily-sales-breakdowns">
          <DailySalesBreakdown title="Theo loại vé" items={report.ticketTypes} emptyText="Chưa có số liệu theo loại vé." />
          <DailySalesBreakdown title="Theo phương thức thanh toán" items={report.paymentMethods} emptyText="Chưa có số liệu theo phương thức thanh toán." />
        </div>
      </> : null}
    </div>
  </div>
}
