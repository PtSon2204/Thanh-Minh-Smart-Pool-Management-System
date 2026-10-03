import { useState } from 'react'
import { 
  PlusOutlined, 
  ReloadOutlined, 
  GiftOutlined,
  SearchOutlined,
  FilterOutlined 
} from '@ant-design/icons'
import { 
  Button, Empty, Pagination, Select, Spin, Table, Tag, Input, Space, Row, Col, Typography, Card, Badge, Tooltip 
} from 'antd'
import VoucherFormModal from '../../features/vouchers/components/VoucherFormModal'
import { useVouchers } from '../../features/vouchers/hooks/useVouchers'
import { DiscountTypeColors, DiscountTypeLabels, DiscountType } from '../../features/vouchers/types/voucher'
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

  const vouchersQuery = useVouchers({ searchTerm: searchTerm || undefined, discountType, isActive, pageIndex, pageSize })

  const resetPage = (setter) => (value) => {
    setter(value)
    setPageIndex(1)
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
            fontFamily: 'monospace',
            fontWeight: 'bold',
            fontSize: '14px',
            color: '#1890ff',
            backgroundColor: '#e6f7ff',
            padding: '4px 12px',
            borderRadius: '6px',
            border: '1px dashed #91d5ff'
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
      width: '15%',
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
      width: '15%',
      render: (value, record) => (
        <Text strong style={{ fontSize: '15px', color: '#cf1322' }}>
          {record.discountType === DiscountType.PERCENTAGE ? `${value}%` : currencyFormatter.format(value)}
        </Text>
      )
    },
    { 
      title: 'Thời gian áp dụng', 
      key: 'time', 
      width: '30%',
      render: (_, record) => {
        if (!record.startDate && !record.endDate) return <Text type="secondary">Không giới hạn</Text>;
        const start = record.startDate ? dayjs(record.startDate).format('DD/MM/YY HH:mm') : '...';
        const end = record.endDate ? dayjs(record.endDate).format('DD/MM/YY HH:mm') : '...';
        return (
          <Space direction="vertical" size={0}>
            <Text type="secondary" style={{ fontSize: '12px' }}>Từ: {start}</Text>
            <Text type="secondary" style={{ fontSize: '12px' }}>Đến: {end}</Text>
          </Space>
        );
      }
    },
    { 
      title: 'Trạng thái', 
      dataIndex: 'isActive', 
      key: 'isActive', 
      align: 'center',
      width: '15%',
      render: (value) => (
        <Tag 
          color={value ? 'success' : 'default'} 
          bordered={false}
          style={{ padding: '4px 12px', borderRadius: '12px', fontWeight: 'bold' }}
        >
          {value ? 'ĐANG HOẠT ĐỘNG' : 'ĐÃ TẠM DỪNG'}
        </Tag>
      ) 
    }
  ]

  return (
    <div style={{ padding: '24px 24px 0', minHeight: '100%', backgroundColor: '#f0f2f5' }}>
      <Card 
        bordered={false} 
        style={{ borderRadius: '12px', boxShadow: '0 4px 20px rgba(0,0,0,0.05)' }}
        styles={{ body: { padding: '24px' } }}
      >
        <Row justify="space-between" align="middle" style={{ marginBottom: 32 }}>
          <Col>
            <Space align="center" size="middle">
              <div style={{ 
                width: 48, height: 48, borderRadius: '12px', 
                background: 'linear-gradient(135deg, #1890ff 0%, #722ed1 100%)',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                boxShadow: '0 4px 12px rgba(24, 144, 255, 0.3)'
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
              onClick={() => setFormOpen(true)}
              style={{ 
                borderRadius: '8px', 
                fontWeight: 600,
                background: 'linear-gradient(90deg, #1890ff 0%, #096dd9 100%)',
                border: 'none',
                boxShadow: '0 4px 10px rgba(24, 144, 255, 0.3)'
              }}
            >
              Phát hành mã mới
            </Button>
          </Col>
        </Row>

        <div style={{ 
          background: '#fafafa', 
          padding: '16px 24px', 
          borderRadius: '8px', 
          marginBottom: 24,
          border: '1px solid #f0f0f0'
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
                  <Text type="secondary"><FilterOutlined /> Loại giảm giá:</Text>
                  <Select
                    size="large"
                    allowClear
                    placeholder="Tất cả"
                    style={{ width: 160 }}
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
                      { label: 'Đã tạm dừng', value: false }
                    ]}
                    value={isActive}
                    onChange={resetPage(setIsActive)}
                  />
                </Space>
              </Space>
            </Col>
          </Row>
        </div>

        <section aria-label="Danh sách voucher">
          {vouchersQuery.isLoading && <div style={{ textAlign: 'center', padding: '50px 0' }}><Spin size="large" /></div>}
          {vouchersQuery.isError && <RetryEmpty onRetry={vouchersQuery.refetch} />}
          {!vouchersQuery.isLoading && !vouchersQuery.isError && (
            <Table
              rowKey="id"
              columns={columns}
              dataSource={vouchersQuery.data?.items || []}
              pagination={false}
              scroll={{ x: 800 }}
              rowClassName={() => 'voucher-table-row'}
              locale={{ emptyText: 'Không tìm thấy voucher phù hợp.' }}
            />
          )}
          {!vouchersQuery.isLoading && !vouchersQuery.isError && (vouchersQuery.data?.totalCount || 0) > 0 && (
            <div style={{ marginTop: 24, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Text type="secondary">
                Hiển thị {(pageIndex - 1) * pageSize + 1} - {Math.min(pageIndex * pageSize, vouchersQuery.data.totalCount)} trong tổng số {vouchersQuery.data.totalCount} voucher
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

      <VoucherFormModal open={formOpen} onClose={() => setFormOpen(false)} />
      
      <style dangerouslySetInnerHTML={{__html: `
        .voucher-table-row:hover > td {
          background-color: #f8fbff !important;
        }
        .ant-table-thead > tr > th {
          background-color: #fafafa;
          color: #595959;
          font-weight: 600;
        }
      `}} />
    </div>
  )
}
