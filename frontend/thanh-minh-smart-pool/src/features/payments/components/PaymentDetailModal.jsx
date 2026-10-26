import { Modal, Descriptions, Table, Tag, Typography, Spin, Alert, Space, Card, Row, Col } from 'antd'
import {
  CreditCardOutlined,
  MoneyCollectOutlined,
  UserOutlined,
  TeamOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ClockCircleOutlined,
  QrcodeOutlined
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { usePaymentDetail } from '../hooks/usePayments'

const { Title, Text } = Typography

const formatCurrency = (val) => {
  if (val === null || val === undefined) return '0 ₫'
  return new Intl.NumberFormat('vi-VN').format(val) + ' ₫'
}

export default function PaymentDetailModal({ paymentId, open, onCancel }) {
  const { data, isLoading, error } = usePaymentDetail(paymentId)

  const isSuccess = data?.status === 'COMPLETED'
  const isFailed = data?.status === 'FAILED'
  const isTransfer = data?.paymentMethod === 'BANK_TRANSFER' || data?.paymentMethod === 'TRANSFER'

  const itemColumns = [
    {
      title: 'Vé / Dịch vụ thuê',
      dataIndex: 'itemName',
      key: 'itemName',
      render: (name, r) => (
        <div>
          <Text strong>{name}</Text>
          {r.ticketCategory && (
            <div>
              <Tag color="cyan" style={{ fontSize: '11px', marginTop: 2 }}>
                {r.ticketCategory === 'VE_THANG' ? 'Vé tháng' : 'Vé lượt'}
              </Tag>
            </div>
          )}
        </div>
      ),
    },
    {
      title: 'Số lượng',
      dataIndex: 'quantity',
      key: 'quantity',
      width: 90,
      align: 'center',
    },
    {
      title: 'Đơn giá',
      dataIndex: 'unitPrice',
      key: 'unitPrice',
      width: 140,
      align: 'right',
      render: (price) => formatCurrency(price),
    },
    {
      title: 'Thành tiền',
      key: 'subTotal',
      width: 140,
      align: 'right',
      render: (_, r) => (
        <Text strong style={{ color: '#0284c7' }}>
          {formatCurrency(r.quantity * r.unitPrice)}
        </Text>
      ),
    },
  ]

  const ticketColumns = [
    {
      title: 'Mã QR / Vé',
      dataIndex: 'qrCode',
      key: 'qrCode',
      render: (qr) => (
        <Space>
          <QrcodeOutlined style={{ color: '#0284c7' }} />
          <Text code copyable>{qr}</Text>
        </Space>
      ),
    },
    {
      title: 'Loại vé',
      dataIndex: 'ticketName',
      key: 'ticketName',
    },
    {
      title: 'Ngày hiệu lực',
      key: 'dates',
      render: (_, r) => (
        <Text type="secondary" style={{ fontSize: '12px' }}>
          {r.issueDate ? dayjs(r.issueDate).format('DD/MM/YYYY') : '---'}
          {' → '}
          {r.expiryDate ? dayjs(r.expiryDate).format('DD/MM/YYYY') : '---'}
        </Text>
      ),
    },
    {
      title: 'Lượt còn lại',
      dataIndex: 'remainingEntries',
      key: 'remainingEntries',
      width: 110,
      align: 'center',
      render: (val) => val ?? 'Không giới hạn',
    },
    {
      title: 'Trạng thái',
      dataIndex: 'status',
      key: 'status',
      width: 110,
      render: (st) => (
        <Tag color={st === 'ACTIVE' ? 'success' : 'default'}>
          {st === 'ACTIVE' ? 'Hoạt động' : st}
        </Tag>
      ),
    },
  ]

  return (
    <Modal
      title={
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Title level={4} style={{ margin: 0, color: '#0369a1' }}>
            Chi tiết giao dịch {data?.invoiceCode || ''}
          </Title>
          {data?.status && (
            <Tag
              icon={
                isSuccess ? <CheckCircleOutlined /> : isFailed ? <CloseCircleOutlined /> : <ClockCircleOutlined />
              }
              color={isSuccess ? 'success' : isFailed ? 'error' : 'warning'}
              style={{ borderRadius: 12, padding: '2px 10px' }}
            >
              {isSuccess ? 'Thành công' : isFailed ? 'Thất bại' : 'Chờ xử lý'}
            </Tag>
          )}
        </div>
      }
      open={open}
      onCancel={onCancel}
      footer={null}
      width={850}
      centered
      destroyOnClose
    >
      {isLoading ? (
        <div style={{ textAlign: 'center', padding: '60px 0' }}>
          <Spin size="large" tip="Đang tải dữ liệu từ server..." />
        </div>
      ) : error ? (
        <Alert
          type="error"
          message="Không thể tải thông tin giao dịch"
          description={error.message || 'Vui lòng thử lại sau.'}
          showIcon
          style={{ margin: '20px 0' }}
        />
      ) : data ? (
        <div style={{ maxHeight: '70vh', overflowY: 'auto', paddingRight: 6 }}>
          {/* Card: Thông tin thanh toán & Đơn hàng */}
          <Card size="small" style={{ marginBottom: 16, background: '#f8fafc', borderColor: '#e2e8f0' }}>
            <Descriptions title="1. Thông tin thanh toán & Hóa đơn" bordered size="small" column={{ xs: 1, sm: 2 }}>
              <Descriptions.Item label="Mã hóa đơn">
                <Text strong style={{ color: '#0369a1' }}>{data.invoiceCode}</Text>
              </Descriptions.Item>
              <Descriptions.Item label="Mã đơn hàng">
                <Text code>{data.orderCode}</Text>
              </Descriptions.Item>
              <Descriptions.Item label="Thời gian thanh toán">
                {data.paymentTime ? dayjs(data.paymentTime).format('DD/MM/YYYY HH:mm:ss') : 'Chưa cập nhật'}
              </Descriptions.Item>
              <Descriptions.Item label="Phương thức">
                <Space>
                  {isTransfer ? (
                    <CreditCardOutlined style={{ color: '#0284c7' }} />
                  ) : (
                    <MoneyCollectOutlined style={{ color: '#16a34a' }} />
                  )}
                  <Text strong>
                    {isTransfer ? 'Chuyển khoản' : data.paymentMethod === 'MANUAL' ? 'Nhân viên xác nhận' : data.paymentMethod === 'CASH' ? 'Tiền mặt' : data.paymentMethod}
                  </Text>
                </Space>
              </Descriptions.Item>
              <Descriptions.Item label="Mã tham chiếu (Ref)">
                <Text>{data.transactionRef || '---'}</Text>
              </Descriptions.Item>
              <Descriptions.Item label={data.paymentMethod === 'MANUAL' ? 'Phí thuê đã thu (không gồm cọc)' : 'Số tiền thanh toán'}>
                <Text strong style={{ color: '#ea580c', fontSize: '16px' }}>
                  {formatCurrency(data.amount)}
                </Text>
              </Descriptions.Item>
              <Descriptions.Item label="Tổng tiền đơn hàng">
                {formatCurrency(data.totalAmount)}
              </Descriptions.Item>
              <Descriptions.Item label="Giảm giá voucher">
                {data.discountAmount > 0 ? (
                  <Text type="danger">
                    -{formatCurrency(data.discountAmount)} {data.voucherCode ? `(${data.voucherCode})` : ''}
                  </Text>
                ) : (
                  '0 ₫'
                )}
              </Descriptions.Item>
            </Descriptions>
          </Card>

          {/* Card: Người liên quan (Khách hàng & Người xử lý) */}
          <Row gutter={16} style={{ marginBottom: 16 }}>
            <Col xs={24} sm={12}>
              <Card
                size="small"
                title={
                  <Space>
                    <UserOutlined style={{ color: '#0284c7' }} />
                    <span>Người thanh toán / Khách hàng</span>
                  </Space>
                }
                style={{ height: '100%', borderColor: '#e2e8f0' }}
              >
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <div>
                    <Text type="secondary">Họ và tên: </Text>
                    <Text strong>{data.customerName || 'Khách vãng lai'}</Text>
                  </div>
                  <div>
                    <Text type="secondary">Số điện thoại: </Text>
                    <Text>{data.customerPhone || '---'}</Text>
                  </div>
                  <div>
                    <Text type="secondary">Email: </Text>
                    <Text>{data.customerEmail || '---'}</Text>
                  </div>
                  <div>
                    <Text type="secondary">Địa chỉ: </Text>
                    <Text>{data.customerAddress || '---'}</Text>
                  </div>
                </div>
              </Card>
            </Col>

            <Col xs={24} sm={12}>
              <Card
                size="small"
                title={
                  <Space>
                    <TeamOutlined style={{ color: '#16a34a' }} />
                    <span>Nhân viên xử lý (Bán vé & Thu tiền)</span>
                  </Space>
                }
                style={{ height: '100%', borderColor: '#e2e8f0' }}
              >
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <div>
                    <Text type="secondary">Nhân viên thực hiện: </Text>
                    <Text strong style={{ color: '#15803d' }}>
                      {data.staffName || 'Tự động (Hệ thống / Mua online)'}
                    </Text>
                  </div>
                  <div>
                    <Text type="secondary">Số điện thoại: </Text>
                    <Text>{data.staffPhone || '---'}</Text>
                  </div>
                  <div>
                    <Text type="secondary">Email: </Text>
                    <Text>{data.staffEmail || '---'}</Text>
                  </div>
                </div>
              </Card>
            </Col>
          </Row>

          {/* Card: Chi tiết vé và dịch vụ thuê trong đơn */}
          <Card size="small" title="2. Chi tiết vé / Dịch vụ thuê" style={{ marginBottom: 16, borderColor: '#e2e8f0' }}>
            <Table
              columns={itemColumns}
              scroll={{ x: 560 }}
              dataSource={data.items || []}
              rowKey="id"
              pagination={false}
              size="small"
              bordered
            />
          </Card>

          {/* Card: Vé điện tử / QR Code đã cấp phát (nếu có) */}
          {data.tickets && data.tickets.length > 0 && (
            <Card size="small" title="3. Vé điện tử & QR Code được cấp phát" style={{ borderColor: '#e2e8f0' }}>
              <Table
                columns={ticketColumns}
                scroll={{ x: 640 }}
                dataSource={data.tickets}
                rowKey="ticketId"
                pagination={false}
                size="small"
                bordered
              />
            </Card>
          )}
        </div>
      ) : null}
    </Modal>
  )
}
