import { Button, Empty, Table } from 'antd'
import { EyeOutlined } from '@ant-design/icons'
import { formatVietnamTime } from '../utils/classSchedule'
import SwimClassStatusTag from './SwimClassStatusTag'

const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

export default function SwimClassListTable({ items, loading, filtered, onView }) {
  const columns = [
    { title: 'Lớp học', key: 'name', width: 260, render: (_, item) => <div className="swim-class-list-name"><strong>{item.name}</strong></div> },
    { title: 'Trạng thái', dataIndex: 'status', width: 145, render: status => <SwimClassStatusTag status={status} /> },
    { title: 'Nhận đăng ký', key: 'registration', width: 250, render: (_, item) => <div className="swim-class-list-dates"><span>{formatVietnamTime(item.registrationStartAt)}</span><span>đến {formatVietnamTime(item.registrationEndAt)}</span></div> },
    { title: 'Học phí', dataIndex: 'fee', width: 130, render: fee => currency.format(fee) },
    { title: 'Buổi học', dataIndex: 'sessionCount', width: 100, render: count => count ?? 0 },
    { title: 'Thao tác', key: 'actions', width: 130, render: (_, item) => <Button icon={<EyeOutlined />} onClick={() => onView(item.id)} aria-label={`Xem chi tiết lớp ${item.name}`}>Chi tiết</Button> },
  ]

  return <Table
    rowKey="id"
    columns={columns}
    dataSource={items}
    loading={loading}
    pagination={false}
    scroll={{ x: 1030 }}
    locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={filtered ? 'Không có lớp phù hợp. Thử thay đổi bộ lọc.' : 'Chưa có lớp học nào trong phạm vi của bạn.'} /> }}
  />
}
