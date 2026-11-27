import { Button, Empty, Spin, Table } from 'antd'
import { EyeOutlined } from '@ant-design/icons'
import { formatVietnamTime } from '../utils/classSchedule'
import SwimClassStatusTag from './SwimClassStatusTag'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

export default function SwimClassListTable({ items, loading, filtered, emptyDescription, onView }) {
  const columns = [
    { title: 'Lớp học', key: 'name', width: 260, render: (_, item) => <div className="swim-class-list-name"><strong>{item.name}</strong></div> },
    { title: 'Trạng thái', dataIndex: 'status', width: 145, render: status => <SwimClassStatusTag status={status} /> },
    { title: 'Nhận đăng ký', key: 'registration', width: 250, render: (_, item) => <div className="swim-class-list-dates"><span>{formatVietnamTime(item.registrationStartAt)}</span><span>đến {formatVietnamTime(item.registrationEndAt)}</span></div> },
    { title: 'Học phí', dataIndex: 'fee', width: 130, render: fee => currency.format(fee) },
    { title: 'Buổi học', dataIndex: 'sessionCount', width: 100, render: count => count ?? 0 },
    { title: 'Thao tác', key: 'actions', width: 130, render: (_, item) => <Button icon={<EyeOutlined />} onClick={() => onView(item.id)} aria-label={`Xem chi tiết lớp ${item.name}`}>Chi tiết</Button> },
  ]

  const emptyText = filtered ? 'Không có lớp phù hợp. Thử thay đổi bộ lọc.' : emptyDescription || 'Chưa có lớp học nào trong phạm vi của bạn.'

  return <>
  <div className="swim-class-list-desktop"><Table
    rowKey="id"
    columns={columns}
    dataSource={items}
    loading={loading}
    pagination={false}
    scroll={{ x: 1030 }}
    locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyText} /> }}
  /></div>
  <div className="swim-class-list-mobile" aria-busy={loading}>
    {loading ? <div className="swim-class-list-mobile-loading"><Spin tip="Đang tải lớp học..." /></div>
      : items.length === 0 ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyText} />
        : items.map(item => <article className="swim-class-list-card" key={item.id}>
          <div className="swim-class-list-card-heading"><h3>{item.name}</h3><SwimClassStatusTag status={item.status} /></div>
          <dl>
            <div><dt>Nhận đăng ký</dt><dd>{formatVietnamTime(item.registrationStartAt)}<br />đến {formatVietnamTime(item.registrationEndAt)}</dd></div>
            <div><dt>Học phí</dt><dd>{currency.format(item.fee)}</dd></div>
            <div><dt>Buổi học</dt><dd>{item.sessionCount ?? 0}</dd></div>
          </dl>
          <Button block icon={<EyeOutlined />} onClick={() => onView(item.id)} aria-label={`Xem chi tiết lớp ${item.name}`}>Chi tiết lớp</Button>
        </article>)}
  </div>
  </>
}
