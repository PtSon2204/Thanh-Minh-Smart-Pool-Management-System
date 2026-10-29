import { useState } from 'react'
import {
  Table,
  Tag,
  Button,
  DatePicker,
  Select,
  Space,
  Card,
  Pagination,
  Empty,
} from 'antd'
import {
  EyeOutlined,
  ReloadOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { useInspections } from '../hooks/useInspections'
import DailyInspectionDetailsModal from './DailyInspectionDetailsModal'

const { RangePicker } = DatePicker

export default function DailyInspectionHistory() {
  const [params, setParams] = useState({
    pageIndex: 1,
    pageSize: 10,
    fromDate: undefined,
    toDate: undefined,
    overallStatus: undefined,
  })

  const [selectedInspection, setSelectedInspection] = useState(null)
  const [detailsModalOpen, setDetailsModalOpen] = useState(false)

  const { data, isLoading, isFetching, refetch } = useInspections(params)

  const items = data?.items || []
  const totalCount = data?.totalCount || 0

  const handleDateChange = (dates) => {
    if (dates && dates.length === 2) {
      setParams((prev) => ({
        ...prev,
        fromDate: dates[0].format('YYYY-MM-DD'),
        toDate: dates[1].format('YYYY-MM-DD'),
        pageIndex: 1,
      }))
    } else {
      setParams((prev) => ({
        ...prev,
        fromDate: undefined,
        toDate: undefined,
        pageIndex: 1,
      }))
    }
  }

  const handleStatusChange = (val) => {
    setParams((prev) => ({
      ...prev,
      overallStatus: val || undefined,
      pageIndex: 1,
    }))
  }

  const handlePageChange = (page, pageSize) => {
    setParams((prev) => ({
      ...prev,
      pageIndex: page,
      pageSize,
    }))
  }

  const openDetails = (record) => {
    setSelectedInspection(record)
    setDetailsModalOpen(true)
  }

  const columns = [
    {
      title: 'Thời gian',
      key: 'time',
      width: 150,
      render: (_, record) => (
        <div>
          <strong>{dayjs(record.inspectionDate).format('DD/MM/YYYY')}</strong>
          <div style={{ fontSize: 12, color: '#6b7280' }}>
            {dayjs(record.inspectionTime).format('HH:mm:ss')}
          </div>
        </div>
      ),
    },
    {
      title: 'Ca trực',
      key: 'shift',
      width: 120,
      render: (_, record) => (
        <Tag color={record.shiftName?.includes('sáng') ? 'orange' : 'purple'}>
          {record.shiftName || 'Ca trực'}
        </Tag>
      ),
    },
    {
      title: 'Người kiểm tra',
      dataIndex: 'inspectorName',
      key: 'inspectorName',
      width: 160,
      render: (name) => <strong>{name || 'Nhân viên'}</strong>,
    },
    {
      title: 'Kết quả',
      key: 'overallStatus',
      width: 150,
      render: (_, record) => {
        const isPassed = record.overallStatus === 'Passed'
        return isPassed ? (
          <Tag icon={<CheckCircleOutlined />} color="success">
            Đạt chuẩn
          </Tag>
        ) : (
          <Tag icon={<ExclamationCircleOutlined />} color="error">
            Có sự cố ({record.issuesCount})
          </Tag>
        )
      },
    },
    {
      title: 'Thiết bị kiểm tra',
      key: 'summary',
      width: 160,
      render: (_, record) => (
        <div>
          <span>{record.totalItems} thiết bị</span>
          {record.issuesCount > 0 ? (
            <div style={{ fontSize: 12, color: '#dc2626' }}>
              ⚠️ {record.issuesCount} thiết bị lỗi
            </div>
          ) : (
            <div style={{ fontSize: 12, color: '#16a34a' }}>
              ✓ Tất cả bình thường
            </div>
          )}
        </div>
      ),
    },
    {
      title: 'Ghi chú ca trực',
      dataIndex: 'notes',
      key: 'notes',
      ellipsis: true,
      render: (notes) => notes || <span style={{ color: '#9ca3af' }}>—</span>,
    },
    {
      title: 'Thao tác',
      key: 'actions',
      width: 110,
      align: 'center',
      render: (_, record) => (
        <Button
          type="link"
          icon={<EyeOutlined />}
          onClick={() => openDetails(record)}
        >
          Chi tiết
        </Button>
      ),
    },
  ]

  return (
    <div>
      {/* Bộ lọc */}
      <Card style={{ marginBottom: 16, borderRadius: 10 }}>
        <Space wrap size="middle" style={{ width: '100%', justifyContent: 'space-between' }}>
          <Space wrap size="middle">
            <div>
              <span style={{ marginRight: 8, fontSize: 13, color: '#4b5563' }}>Thời gian:</span>
              <RangePicker
                format="DD/MM/YYYY"
                placeholder={['Từ ngày', 'Đến ngày']}
                onChange={handleDateChange}
              />
            </div>

            <div>
              <span style={{ marginRight: 8, fontSize: 13, color: '#4b5563' }}>Trạng thái:</span>
              <Select
                placeholder="Tất cả trạng thái"
                style={{ width: 170 }}
                allowClear
                value={params.overallStatus}
                onChange={handleStatusChange}
                options={[
                  { label: 'Tất cả kết quả', value: '' },
                  { label: 'Đạt chuẩn (Passed)', value: 'Passed' },
                  { label: 'Có sự cố (ActionRequired)', value: 'ActionRequired' },
                ]}
              />
            </div>
          </Space>

          <Button
            icon={<ReloadOutlined />}
            onClick={() => refetch()}
            loading={isFetching}
          >
            Làm mới
          </Button>
        </Space>
      </Card>

      {/* Bảng dữ liệu */}
      <Card style={{ borderRadius: 10 }}>
        <Table
          dataSource={items}
          columns={columns}
          rowKey="id"
          loading={isLoading}
          pagination={false}
          size="middle"
          locale={{
            emptyText: (
              <Empty
                description="Không tìm thấy lịch sử kiểm tra thiết bị nào"
                style={{ padding: '30px 0' }}
              />
            ),
          }}
        />

        {totalCount > 0 && (
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
            <Pagination
              current={params.pageIndex}
              pageSize={params.pageSize}
              total={totalCount}
              showSizeChanger
              pageSizeOptions={['10', '20', '50']}
              onChange={handlePageChange}
              showTotal={(total, range) => `${range[0]}-${range[1]} / ${total} biên bản`}
            />
          </div>
        )}
      </Card>

      {/* Modal chi tiết */}
      <DailyInspectionDetailsModal
        open={detailsModalOpen}
        inspection={selectedInspection}
        onClose={() => {
          setDetailsModalOpen(false)
          setSelectedInspection(null)
        }}
      />
    </div>
  )
}
