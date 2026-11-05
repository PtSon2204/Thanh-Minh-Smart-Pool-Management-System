import { useState, useEffect } from 'react'
import {
  Card,
  Row,
  Col,
  Statistic,
  Tag,
  Form,
  InputNumber,
  TimePicker,
  Radio,
  Input,
  Button,
  Progress,
  Alert,
  Spin,
  notification,
  Divider,
  Space,
} from 'antd'
import {
  DashboardOutlined,
  UserOutlined,
  ShopOutlined,
  ShoppingOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  SaveOutlined,
  WarningOutlined,
  CheckCircleOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import poolAccessService from '../../../features/tickets/services/poolAccessService'

export default function PoolOperationsPage() {
  const queryClient = useQueryClient()
  const [configForm] = Form.useForm()
  const [statusForm] = Form.useForm()
  const [savingConfig, setSavingConfig] = useState(false)
  const [savingStatus, setSavingStatus] = useState(false)

  const {
    data: liveStatus,
    isLoading,
    isFetching,
    refetch,
  } = useQuery({
    queryKey: ['poolLiveStatus'],
    queryFn: poolAccessService.getLiveStatus,
    refetchInterval: 15000,
  })

  useEffect(() => {
    if (liveStatus) {
      configForm.setFieldsValue({
        maxCapacity: liveStatus.maxCapacity ?? 100,
        warningThresholdPercent: liveStatus.warningThresholdPercent ?? 80,
        morningRange: [
          liveStatus.morningStartTime ? dayjs(liveStatus.morningStartTime, 'HH:mm') : dayjs('06:00', 'HH:mm'),
          liveStatus.morningEndTime ? dayjs(liveStatus.morningEndTime, 'HH:mm') : dayjs('11:30', 'HH:mm'),
        ],
        afternoonRange: [
          liveStatus.afternoonStartTime ? dayjs(liveStatus.afternoonStartTime, 'HH:mm') : dayjs('13:30', 'HH:mm'),
          liveStatus.afternoonEndTime ? dayjs(liveStatus.afternoonEndTime, 'HH:mm') : dayjs('19:30', 'HH:mm'),
        ],
      })

      statusForm.setFieldsValue({
        operationalStatus: liveStatus.operationalStatus || 'Operating',
        statusReason: liveStatus.statusReason || '',
      })
    }
  }, [liveStatus, configForm, statusForm])

  const handleSaveConfig = async (values) => {
    try {
      setSavingConfig(true)
      const payload = {
        maxCapacity: values.maxCapacity,
        warningThresholdPercent: values.warningThresholdPercent,
        morningStartTime: values.morningRange?.[0]?.format('HH:mm') || '06:00',
        morningEndTime: values.morningRange?.[1]?.format('HH:mm') || '11:30',
        afternoonStartTime: values.afternoonRange?.[0]?.format('HH:mm') || '13:30',
        afternoonEndTime: values.afternoonRange?.[1]?.format('HH:mm') || '19:30',
      }

      await poolAccessService.updateOperationalConfig(payload)
      notification.success({
        message: 'Thành công',
        description: 'Đã cập nhật cấu hình sức chứa và khung giờ ca trực hồ bơi.',
      })
      queryClient.invalidateQueries({ queryKey: ['poolLiveStatus'] })
    } catch (err) {
      notification.error({
        message: 'Lỗi lưu cấu hình',
        description: err?.response?.data?.message || err.message,
      })
    } finally {
      setSavingConfig(false)
    }
  }

  const handleSaveStatus = async (values) => {
    try {
      setSavingStatus(true)
      await poolAccessService.toggleMaintenance(values.operationalStatus, values.statusReason)
      notification.success({
        message: 'Thành công',
        description: 'Đã thay đổi trạng thái hoạt động của bể bơi.',
      })
      queryClient.invalidateQueries({ queryKey: ['poolLiveStatus'] })
    } catch (err) {
      notification.error({
        message: 'Lỗi cập nhật trạng thái',
        description: err?.response?.data?.message || err.message,
      })
    } finally {
      setSavingStatus(false)
    }
  }

  const isMaintenance = liveStatus?.operationalStatus === 'Maintenance'
  const occupancyPercent = liveStatus?.occupancyRate || 0

  return (
    <div style={{ padding: '24px', maxWidth: 1200, margin: '0 auto' }}>
      {/* Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 24,
          flexWrap: 'wrap',
          gap: 16,
        }}
      >
        <div>
          <h1 style={{ fontSize: 24, fontWeight: 700, margin: 0, color: '#0f172a', display: 'flex', alignItems: 'center', gap: 10 }}>
            <DashboardOutlined style={{ color: '#0068ff' }} /> Quản lý vận hành bể bơi
          </h1>
          <p style={{ margin: '4px 0 0', color: '#64748b', fontSize: 14 }}>
            Theo dõi sức chứa thời gian thực, lưu lượng khách trong ngày và cấu hình ca trực vận hành.
          </p>
        </div>
        <Button
          icon={<ReloadOutlined spin={isFetching} />}
          onClick={() => refetch()}
          loading={isFetching}
        >
          Làm mới số liệu
        </Button>
      </div>

      {/* Cảnh báo bảo trì nếu bể đang dừng */}
      {isMaintenance && (
        <Alert
          type="error"
          showIcon
          icon={<WarningOutlined style={{ fontSize: 24 }} />}
          message="BỂ BƠI ĐANG TẠM DỪNG HOẠT ĐỘNG (BẢO TRÌ KỸ THUẬT)"
          description={
            <div style={{ marginTop: 6 }}>
              <p style={{ margin: 0 }}>
                <strong>Lý do tạm dừng:</strong> {liveStatus.statusReason || 'Đang bảo dưỡng định kỳ hoặc kiểm tra an toàn theo quy định.'}
              </p>
              <p style={{ margin: '4px 0 0', fontSize: 13, color: '#b91c1c' }}>
                Hệ thống đang khóa tính năng thanh toán vé online trên trang đặt vé và hiển thị banner thông báo bảo trì tại trang chủ.
              </p>
            </div>
          }
          style={{ marginBottom: 24, borderRadius: 8, border: '1px solid #fca5a5' }}
        />
      )}

      {/* Cards thống kê thời gian thực */}
      <Spin spinning={isLoading}>
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          {/* Card 1: Khách đang bơi */}
          <Col xs={24} sm={12} lg={6}>
            <Card
              bordered={false}
              style={{
                borderRadius: 12,
                boxShadow: '0 1px 3px rgba(0,0,0,0.08)',
                background: liveStatus?.isCrowded ? '#fffbeb' : '#fff',
                border: liveStatus?.isCrowded ? '1px solid #fde68a' : 'none',
              }}
            >
              <Statistic
                title={<span style={{ fontWeight: 600, color: '#475569' }}>Khách đang trong bể</span>}
                value={liveStatus?.currentSwimmers ?? 0}
                suffix={<span style={{ fontSize: 14, color: '#64748b' }}>/ {liveStatus?.maxCapacity ?? 100} người</span>}
                prefix={<UserOutlined style={{ color: liveStatus?.isCrowded ? '#f59e0b' : '#0068ff' }} />}
              />
              <div style={{ marginTop: 12 }}>
                <Progress
                  percent={occupancyPercent}
                  status={liveStatus?.isCrowded ? 'exception' : 'active'}
                  strokeColor={liveStatus?.isCrowded ? '#f59e0b' : '#0068ff'}
                  size="small"
                />
              </div>
              <div style={{ marginTop: 6, fontSize: 12 }}>
                {liveStatus?.isCrowded ? (
                  <Tag color="warning">⚠️ Đang đông khách ({occupancyPercent}%)</Tag>
                ) : (
                  <Tag color="success">🟢 Mật độ bình thường ({occupancyPercent}%)</Tag>
                )}
              </div>
            </Card>
          </Col>

          {/* Card 2: Vé bán tại quầy hôm nay */}
          <Col xs={24} sm={12} lg={6}>
            <Card bordered={false} style={{ borderRadius: 12, boxShadow: '0 1px 3px rgba(0,0,0,0.08)' }}>
              <Statistic
                title={<span style={{ fontWeight: 600, color: '#475569' }}>Vé tại quầy hôm nay (POS)</span>}
                value={liveStatus?.counterTicketsToday ?? 0}
                suffix="lượt"
                prefix={<ShopOutlined style={{ color: '#10b981' }} />}
              />
              <div style={{ marginTop: 16, fontSize: 13, color: '#64748b' }}>
                Giao dịch bán vé trực tiếp cho khách tại quầy soát vé trong ngày.
              </div>
            </Card>
          </Col>

          {/* Card 3: Vé mua online hôm nay */}
          <Col xs={24} sm={12} lg={6}>
            <Card bordered={false} style={{ borderRadius: 12, boxShadow: '0 1px 3px rgba(0,0,0,0.08)' }}>
              <Statistic
                title={<span style={{ fontWeight: 600, color: '#475569' }}>Vé đặt online hôm nay</span>}
                value={liveStatus?.onlineTicketsToday ?? 0}
                suffix="lượt"
                prefix={<ShoppingOutlined style={{ color: '#8b5cf6' }} />}
              />
              <div style={{ marginTop: 16, fontSize: 13, color: '#64748b' }}>
                Số lượt vé khách hàng đã đặt và thanh toán online qua website.
              </div>
            </Card>
          </Col>

          {/* Card 4: Ca trực & Trạng thái hiện tại */}
          <Col xs={24} sm={12} lg={6}>
            <Card bordered={false} style={{ borderRadius: 12, boxShadow: '0 1px 3px rgba(0,0,0,0.08)' }}>
              <div style={{ fontSize: 14, color: '#475569', fontWeight: 600, marginBottom: 8 }}>
                Ca trực & Giờ mở cửa
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
                <ClockCircleOutlined style={{ color: '#0284c7', fontSize: 18 }} />
                <strong style={{ fontSize: 16, color: '#0f172a' }}>
                  {liveStatus?.currentShiftName || 'Đang cập nhật'}
                </strong>
              </div>
              <div style={{ marginTop: 10 }}>
                {isMaintenance ? (
                  <Tag color="error">🔴 Tạm dừng bảo trì</Tag>
                ) : liveStatus?.effectiveStatus === 'Operating' ? (
                  <Tag color="success">🟢 Đang mở cửa đón khách</Tag>
                ) : liveStatus?.effectiveStatus === 'BreakTime' ? (
                  <Tag color="warning">🟡 Đang nghỉ trưa</Tag>
                ) : (
                  <Tag color="default">⚪ Ngoài giờ hoạt động</Tag>
                )}
              </div>
            </Card>
          </Col>
        </Row>
      </Spin>

      {/* Khu vực cấu hình chi tiết */}
      <Row gutter={[24, 24]}>
        {/* Cột 1: Cấu hình sức chứa & ca trực */}
        <Col xs={24} lg={14}>
          <Card
            title={
              <Space>
                <ToolOutlined style={{ color: '#0068ff' }} />
                <span>Cấu hình Sức chứa & Khung giờ ca trực</span>
              </Space>
            }
            bordered={false}
            style={{ borderRadius: 12, boxShadow: '0 1px 3px rgba(0,0,0,0.08)' }}
          >
            <Form form={configForm} layout="vertical" onFinish={handleSaveConfig}>
              <Row gutter={16}>
                <Col xs={24} sm={12}>
                  <Form.Item
                    label="Sức chứa tối đa (người)"
                    name="maxCapacity"
                    rules={[{ required: true, message: 'Vui lòng nhập sức chứa tối đa' }]}
                    extra="Số người tối đa được phép có mặt cùng lúc trong bể bơi"
                  >
                    <InputNumber min={10} max={1000} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col xs={24} sm={12}>
                  <Form.Item
                    label="Ngưỡng cảnh báo đông (%)"
                    name="warningThresholdPercent"
                    rules={[{ required: true, message: 'Vui lòng nhập ngưỡng cảnh báo' }]}
                    extra="Khi đạt tỷ lệ này, hệ thống sẽ bật cảnh báo đông tại trang mua vé"
                  >
                    <InputNumber min={50} max={100} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
              </Row>

              <Divider style={{ margin: '12px 0 20px' }} />

              <Row gutter={16}>
                <Col xs={24} sm={12}>
                  <Form.Item
                    label="Khung giờ Ca sáng"
                    name="morningRange"
                    rules={[{ required: true, message: 'Vui lòng chọn khung giờ ca sáng' }]}
                    extra="Mặc định: 06:00 – 11:30"
                  >
                    <TimePicker.RangePicker format="HH:mm" style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col xs={24} sm={12}>
                  <Form.Item
                    label="Khung giờ Ca chiều"
                    name="afternoonRange"
                    rules={[{ required: true, message: 'Vui lòng chọn khung giờ ca chiều' }]}
                    extra="Mặc định: 13:30 – 19:30"
                  >
                    <TimePicker.RangePicker format="HH:mm" style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
              </Row>

              <div style={{ textAlign: 'right', marginTop: 12 }}>
                <Button type="primary" htmlType="submit" icon={<SaveOutlined />} loading={savingConfig}>
                  Lưu cấu hình vận hành
                </Button>
              </div>
            </Form>
          </Card>
        </Col>

        {/* Cột 2: Quản lý trạng thái hoạt động bể */}
        <Col xs={24} lg={10}>
          <Card
            title={
              <Space>
                <WarningOutlined style={{ color: isMaintenance ? '#ef4444' : '#10b981' }} />
                <span>Trạng thái Hoạt động Bể bơi</span>
              </Space>
            }
            bordered={false}
            style={{ borderRadius: 12, boxShadow: '0 1px 3px rgba(0,0,0,0.08)' }}
          >
            <Form form={statusForm} layout="vertical" onFinish={handleSaveStatus}>
              <Form.Item
                label="Chế độ vận hành"
                name="operationalStatus"
                rules={[{ required: true, message: 'Vui lòng chọn trạng thái vận hành' }]}
              >
                <Radio.Group style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                  <Radio value="Operating">
                    <span style={{ fontWeight: 600, color: '#16a34a' }}>🟢 Hoạt động đón khách bình thường</span>
                    <div style={{ fontSize: 12, color: '#64748b', paddingLeft: 24 }}>
                      Khách hàng có thể mua vé online và vào bơi theo khung giờ ca trực.
                    </div>
                  </Radio>
                  <Radio value="Maintenance">
                    <span style={{ fontWeight: 600, color: '#dc2626' }}>🔴 Tạm dừng hoạt động (Bảo trì khẩn cấp)</span>
                    <div style={{ fontSize: 12, color: '#64748b', paddingLeft: 24 }}>
                      Tạm ngưng bán vé online, hiển thị cảnh báo đỏ bảo trì tại trang chủ và giỏ hàng.
                    </div>
                  </Radio>
                  <Radio value="Closed">
                    <span style={{ fontWeight: 600, color: '#64748b' }}>⚪ Đóng cửa bể bơi</span>
                    <div style={{ fontSize: 12, color: '#64748b', paddingLeft: 24 }}>
                      Áp dụng khi bể nghỉ lễ hoặc ngừng hoạt động dài ngày.
                    </div>
                  </Radio>
                </Radio.Group>
              </Form.Item>

              <Form.Item
                label="Lý do / Ghi chú bảo trì"
                name="statusReason"
                extra="Nội dung này sẽ hiển thị cho khách hàng xem trên banner trang chủ"
              >
                <Input.TextArea
                  rows={3}
                  placeholder="Ví dụ: Tạm dừng xử lý sự cố máy bơm lọc nước / thay mới phao cứu sinh..."
                  maxLength={500}
                  showCount
                />
              </Form.Item>

              <div style={{ textAlign: 'right', marginTop: 12 }}>
                <Button
                  danger={isMaintenance}
                  type="primary"
                  htmlType="submit"
                  icon={<CheckCircleOutlined />}
                  loading={savingStatus}
                >
                  Cập nhật trạng thái bể bơi
                </Button>
              </div>
            </Form>
          </Card>
        </Col>
      </Row>
    </div>
  )
}
