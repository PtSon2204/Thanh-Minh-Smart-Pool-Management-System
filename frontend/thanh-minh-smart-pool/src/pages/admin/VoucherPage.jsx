import { useState } from 'react'
import {
  PlusOutlined,
  ReloadOutlined,
  GiftOutlined,
  SearchOutlined,
  FilterOutlined,
  EditOutlined,
  LockOutlined,
  UnlockOutlined,
  DeleteOutlined
} from '@ant-design/icons'
import {
  Button, Empty, Pagination, Select, Spin, Table, Tag, Input,
  Space, Row, Col, Typography, Card, Badge, Modal, Tooltip
} from 'antd'
import VoucherFormModal from '../../features/vouchers/components/VoucherFormModal'
import { useVouchers } from '../../features/vouchers/hooks/useVouchers'
import { useToggleLockVoucher, useDeleteVoucher } from '../../features/vouchers/hooks/useVoucherMutations'
import { DiscountTypeLabels, DiscountType } from '../../features/vouchers/types/voucher'
import dayjs from 'dayjs'

const { Title, Text } = Typography

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function RetryEmpty({ onRetry }) {
  return (
    <Empty
      image={Empty.PRESENTED_IMAGE_SIMPLE}
      description={<Text type="secondary">Không thể tải danh sách voucher.</Text>}
    >
      <Button type="primary" ghost icon={<ReloadOutlined />} onClick={onRetry}>Thử lại</Button>
    </Empty>
  )
}

export default function VoucherPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [discountType, setDiscountType] = useState()
  const [isActive, setIsActive] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [formOpen, setFormOpen] = useState(false)
  const [editingVoucher, setEditingVoucher] = useState(null)

  const vouchersQuery = useVouchers({ searchTerm: searchTerm || undefined, discountType, isActive, pageIndex, pageSize })
  const { mutate: toggleLock, isPending: isToggling } = useToggleLockVoucher()
  const { mutate: deleteVoucher, isPending: isDeleting } = useDeleteVoucher()

  const resetPage = (setter) => (value) => { setter(value); setPageIndex(1) }

  const handleEdit = (record) => {
    setEditingVoucher(record)
    setFormOpen(true)
  }

  const handleCloseModal = () => {
    setFormOpen(false)
    setEditingVoucher(null)
  }

  const handleToggleLock = (record) => {
    const action = record.isActive ? 'khóa' : 'kích hoạt'
    Modal.confirm({
      title: `Xác nhận ${action} voucher`,
      content: (
        <span>
          Bạn muốn <strong>{action}</strong> mã{' '}
          <Text code style={{ color: '#1890ff' }}>{record.code}</Text>?
        </span>
      ),
      okText: action.charAt(0).toUpperCase() + action.slice(1),
      cancelText: 'Hủy',
      okButtonProps: { danger: record.isActive },
      onOk: () => toggleLock(record.id),
    })
  }

  const handleDelete = (record) => {
    Modal.confirm({
      title: 'Xác nhận xóa voucher',
      content: (
        <span>
          Bạn chắc chắn muốn xóa mã{' '}
          <Text code style={{ color: '#cf1322' }}>{record.code}</Text>?
          Thao tác này không thể hoàn tác.
        </span>
      ),
      okText: 'Xóa',
      okType: 'danger',
      cancelText: 'Hủy',
      onOk: () => deleteVoucher(record.id),
    })
  }

  const columns = [
    {
      title: 'Mã Voucher',
      dataIndex: 'code',
      key: 'code',
      width: '18%',
      render: (code) => (
        <Typography.Text
          copyable
          style={{
            fontFamily: 'monospace', fontWeight: 'bold', fontSize: '13px',
            color: '#1890ff', backgroundColor: '#e6f7ff',
            padding: '4px 10px', borderRadius: '6px', border: '1px dashed #91d5ff'
          }}
        >
          {code}
        </Typography.Text>
      )
    },
    {
      title: 'Loại giảm',
      dataIndex: 'discountType',
      key: 'discountType',
      width: '14%',
      render: (type) => (
        <Badge
          color={type === DiscountType.PERCENTAGE ? 'purple' : 'cyan'}
          text={
            <Text strong style={{ color: type === DiscountType.PERCENTAGE ? '#722ed1' : '#13c2c2' }}>
              {DiscountTypeLabels[type] || type}
            </Text>
          }
        />
      )
    },
    {
      title: 'Mức giảm',
      dataIndex: 'discountValue',
      key: 'discountValue',
      align: 'right',
      width: '13%',
      render: (value, record) => (
        <Text strong style={{ fontSize: '14px', color: '#cf1322' }}>
          {record.discountType === DiscountType.PERCENTAGE ? `${value}%` : currencyFormatter.format(value)}
        </Text>
      )
    },
    {
      title: 'Thời hạn',
      key: 'time',
      width: '22%',
      render: (_, record) => {
        if (!record.startDate && !record.endDate) return <Text type="secondary">Không giới hạn</Text>
        const start = record.startDate ? dayjs(record.startDate).format('DD/MM/YY HH:mm') : '...'
        const end = record.endDate ? dayjs(record.endDate).format('DD/MM/YY HH:mm') : '...'
        return (
          <Space direction="vertical" size={0}>
            <Text type="secondary" style={{ fontSize: '12px' }}>Từ: {start}</Text>
            <Text type="secondary" style={{ fontSize: '12px' }}>Đến: {end}</Text>
          </Space>
        )
      }
    },
    {
      title: 'Trạng thái',
      dataIndex: 'isActive',
      key: 'isActive',
      align: 'center',
      width: '13%',
      render: (value) => (
        <Tag
          color={value ? 'success' : 'default'}
          bordered={false}
          style={{ padding: '3px 10px', borderRadius: '12px', fontWeight: 600, fontSize: '11px' }}
        >
          {value ? 'HOẠT ĐỘNG' : 'ĐÃ KHÓA'}
        </Tag>
      )
    },
    {
      title: 'Thao tác',
      key: 'actions',
      align: 'center',
      width: '20%',
      render: (_, record) => (
        <Space size={4}>
          <Tooltip title="Chỉnh sửa">
            <Button
              type="text"
              icon={<EditOutlined />}
              onClick={() => handleEdit(record)}
              style={{ color: '#1890ff' }}
            />
          </Tooltip>
          <Tooltip title={record.isActive ? 'Khóa voucher' : 'Mở khóa voucher'}>
            <Button
              type="text"
              icon={record.isActive ? <LockOutlined /> : <UnlockOutlined />}
              onClick={() => handleToggleLock(record)}
              loading={isToggling}
              style={{ color: record.isActive ? '#fa8c16' : '#52c41a' }}
            />
          </Tooltip>
          <Tooltip title="Xóa voucher">
            <Button
              type="text"
              danger
              icon={<DeleteOutlined />}
              onClick={() => handleDelete(record)}
              loading={isDeleting}
            />
          </Tooltip>
        </Space>
      )
    },
  ]

  return (
    <div style={{ padding: '24px 24px 0', minHeight: '100%', backgroundColor: '#f0f2f5' }}>
      <Card
        bordered={false}
        style={{ borderRadius: '12px', boxShadow: '0 4px 20px rgba(0,0,0,0.05)' }}
        styles={{ body: { padding: '24px' } }}
      >
        {/* Header */}
        <Row justify="space-between" align="middle" style={{ marginBottom: 32 }}>
          <Col>
            <Space align="center" size="middle">
              <div style={{
                width: 48, height: 48, borderRadius: '12px',
                background: 'linear-gradient(135deg, #36cfc9 0%, #096dd9 100%)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                boxShadow: '0 4px 12px rgba(54, 207, 201, 0.35)'
              }}>
                <GiftOutlined style={{ fontSize: 24, color: '#fff' }} />
              </div>
              <div>
                <Title level={3} style={{ margin: 0, fontWeight: 700 }}>Kho Voucher</Title>
                <Text type="secondary">Quản lý và phát hành các mã giảm giá cho khách hàng</Text>
              </div>
            </Space>
          </Col>
          <Col>
            <Button
              type="primary"
              size="large"
              icon={<PlusOutlined />}
              onClick={() => { setEditingVoucher(null); setFormOpen(true) }}
              style={{
                borderRadius: '8px', fontWeight: 600,
                background: 'linear-gradient(90deg, #13c2c2 0%, #1890ff 100%)',
                border: 'none', boxShadow: '0 4px 10px rgba(19, 194, 194, 0.35)'
              }}
            >
              Phát hành mã mới
            </Button>
          </Col>
        </Row>

        {/* Toolbar */}
        <div style={{
          background: '#fafafa', padding: '16px 24px',
          borderRadius: '8px', marginBottom: 24, border: '1px solid #f0f0f0'
        }}>
          <Row gutter={[24, 16]} align="middle">
            <Col xs={24} md={8}>
              <Input
                size="large"
                placeholder="Tìm mã giảm giá (VD: SUMMER2026)"
                prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
                allowClear
                onChange={(e) => resetPage(setSearchTerm)(e.target.value)}
                style={{ borderRadius: '8px' }}
              />
            </Col>
            <Col xs={24} md={16}>
              <Space size="large" wrap>
                <Space>
                  <Text type="secondary"><FilterOutlined /> Loại giảm:</Text>
                  <Select
                    size="large"
                    allowClear
                    placeholder="Tất cả"
                    style={{ width: 170 }}
                    options={[
                      { label: 'Phần trăm (%)', value: DiscountType.PERCENTAGE },
                      { label: 'Số tiền cố định', value: DiscountType.FIXED_AMOUNT }
                    ]}
                    value={discountType}
                    onChange={resetPage(setDiscountType)}
                  />
                </Space>
                <Space>
                  <Text type="secondary">Trạng thái:</Text>
                  <Select
                    size="large"
                    allowClear
                    placeholder="Tất cả"
                    style={{ width: 140 }}
                    options={[
                      { label: 'Đang hoạt động', value: true },
                      { label: 'Đã khóa', value: false }
                    ]}
                    value={isActive}
                    onChange={resetPage(setIsActive)}
                  />
                </Space>
              </Space>
            </Col>
          </Row>
        </div>

        {/* Table */}
        <section>
          {vouchersQuery.isLoading && <div style={{ textAlign: 'center', padding: '50px 0' }}><Spin size="large" /></div>}
          {vouchersQuery.isError && <RetryEmpty onRetry={vouchersQuery.refetch} />}
          {!vouchersQuery.isLoading && !vouchersQuery.isError && (
            <Table
              rowKey="id"
              columns={columns}
              dataSource={vouchersQuery.data?.items || []}
              pagination={false}
              scroll={{ x: 800 }}
              locale={{ emptyText: 'Không tìm thấy voucher phù hợp.' }}
            />
          )}
          {!vouchersQuery.isLoading && !vouchersQuery.isError && (vouchersQuery.data?.totalCount || 0) > 0 && (
            <div style={{ marginTop: 24, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Text type="secondary">
                Hiển thị {(pageIndex - 1) * pageSize + 1}–{Math.min(pageIndex * pageSize, vouchersQuery.data.totalCount)} trong {vouchersQuery.data.totalCount} voucher
              </Text>
              <Pagination
                current={pageIndex}
                pageSize={pageSize}
                total={vouchersQuery.data.totalCount}
                showSizeChanger
                pageSizeOptions={['10', '20', '50']}
                onChange={(page, size) => { setPageIndex(page); setPageSize(size) }}
              />
            </div>
          )}
        </section>
      </Card>

      <VoucherFormModal
        open={formOpen}
        onClose={handleCloseModal}
        editingVoucher={editingVoucher}
      />

      <style dangerouslySetInnerHTML={{__html: `
        .ant-table-tbody > tr:hover > td { background-color: #f0f9ff !important; }
        .ant-table-thead > tr > th { background-color: #fafafa; color: #595959; font-weight: 600; }
      `}} />
    </div>
  )
}
