import { Modal, Descriptions, Tag, Table, Typography } from 'antd'
import dayjs from 'dayjs'
import { CheckCircleOutlined, ExclamationCircleOutlined } from '@ant-design/icons'

const { Text } = Typography

export default function DailyInspectionDetailsModal({ open, inspection, onClose }) {
  if (!inspection) return null

  const isPassed = inspection.overallStatus === 'Passed'

  const columns = [
    {
      title: 'Thiết bị',
      key: 'equipmentName',
      render: (_, item) => (
        <div>
          <strong>{item.equipmentName}</strong>
          {item.location && <div style={{ fontSize: 12, color: '#6b7280' }}>{item.location}</div>}
        </div>
      ),
    },
    {
      title: 'Danh mục',
      dataIndex: 'category',
      key: 'category',
      width: 130,
      render: (cat) => <Tag color="blue">{cat}</Tag>,
    },
    {
      title: 'Đánh giá',
      key: 'isPassed',
      width: 140,
      render: (_, item) =>
        item.isPassed ? (
          <Tag icon={<CheckCircleOutlined />} color="success">
            Hoạt động tốt
          </Tag>
        ) : (
          <Tag icon={<ExclamationCircleOutlined />} color="error">
            Có vấn đề
          </Tag>
        ),
    },
    {
      title: 'Ghi chú sự cố',
      dataIndex: 'note',
      key: 'note',
      render: (note, item) =>
        !item.isPassed ? (
          <Text type="danger">{note || 'Không có mô tả chi tiết'}</Text>
        ) : (
          <span style={{ color: '#9ca3af' }}>—</span>
        ),
    },
  ]

  return (
    <Modal
      title="Chi tiết biên bản kiểm tra thiết bị đầu ca"
      open={open}
      onCancel={onClose}
      footer={null}
      width={750}
      destroyOnClose
    >
      <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} style={{ marginBottom: 16 }}>
        <Descriptions.Item label="Ca trực">
          <Tag color={inspection.shiftName?.includes('sáng') ? 'orange' : 'purple'}>
            <strong>{inspection.shiftName || 'Ca trực'}</strong>
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="Người kiểm tra">
          <strong>{inspection.inspectorName || 'Nhân viên'}</strong>
        </Descriptions.Item>
        <Descriptions.Item label="Thời gian">
          {dayjs(inspection.inspectionTime).format('HH:mm - DD/MM/YYYY')}
        </Descriptions.Item>
        <Descriptions.Item label="Kết quả chung">
          <Tag color={isPassed ? 'success' : 'error'}>
            {isPassed ? 'Đạt chuẩn vận hành' : 'Phát hiện sự cố (Cần xử lý)'}
          </Tag>
        </Descriptions.Item>
        <Descriptions.Item label="Thống kê" span={2}>
          <span>{inspection.totalItems} thiết bị ({inspection.issuesCount} hỏng hóc)</span>
        </Descriptions.Item>
        {inspection.notes && (
          <Descriptions.Item label="Ghi chú ca trực" span={2}>
            {inspection.notes}
          </Descriptions.Item>
        )}
      </Descriptions>

      <Typography.Title level={5} style={{ marginBottom: 8 }}>
        Danh sách thiết bị kiểm tra ({inspection.items?.length || 0})
      </Typography.Title>
      <Table
        dataSource={inspection.items || []}
        columns={columns}
        rowKey={(item, idx) => item.equipmentId || idx}
        pagination={false}
        size="small"
      />
    </Modal>
  )
}
