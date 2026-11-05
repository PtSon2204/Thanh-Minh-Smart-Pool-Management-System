import { useState, useEffect } from 'react'
import {
  Modal,
  Form,
  InputNumber,
  TimePicker,
  Radio,
  Input,
  Button,
  Card,
  Row,
  Col,
  Statistic,
  Tag,
  notification,
  Divider,
} from 'antd'
import {
  SettingOutlined,
  DashboardOutlined,
  UserOutlined,
  ShoppingOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import poolAccessService from '../../tickets/services/poolAccessService'

export default function PoolOperationalSettingsModal({ open, onClose }) {
  const queryClient = useQueryClient()
  const [form] = Form.useForm()
  const [submitting, setSubmitting] = useState(false)

  const { data: liveStatus, isLoading, refetch } = useQuery({
    queryKey: ['poolLiveStatus'],
    queryFn: poolAccessService.getLiveStatus,
    enabled: open,
    refetchInterval: open ? 10000 : false,
  })

  useEffect(() => {
    if (liveStatus) {
      form.setFieldsValue({
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
        operationalStatus: liveStatus.operationalStatus || 'Operating',
        statusReason: liveStatus.statusReason || '',
      })
    }
  }, [liveStatus, form])

  const handleSave = async () => {
    try {
      const values = await form.validateFields()
      setSubmitting(true)

      const payload = {
        maxCapacity: values.maxCapacity,
        warningThresholdPercent: values.warningThresholdPercent,
        morningStartTime: values.morningRange?.[0]?.format('HH:mm') || '06:00',
        morningEndTime: values.morningRange?.[1]?.format('HH:mm') || '11:30',
        afternoonStartTime: values.afternoonRange?.[0]?.format('HH:mm') || '13:30',
        afternoonEndTime: values.afternoonRange?.[1]?.format('HH:mm') || '19:30',
      }

      await poolAccessService.updateOperationalConfig(payload)

      // Cập nhật trạng thái bảo trì nếu có thay đổi
      if (values.operationalStatus !== liveStatus?.operationalStatus || values.statusReason !== liveStatus?.statusReason) {
        await poolAccessService.toggleMaintenance(values.operationalStatus, values.statusReason)
      }

      notification.success({
        message: 'Thành công',
        description: 'Đã lưu cấu hình vận hành và đồng bộ trạng thái bể bơi.',
      })

      queryClient.invalidateQueries({ queryKey: ['poolLiveStatus'] })
      queryClient.invalidateQueries({ queryKey: ['poolOccupancy'] })
      onClose()
    } catch (err) {
      notification.error({
        message: 'Lỗi lưu cấu hình',
        description: err?.response?.data?.message || err.message,
      })
    } finally {
      setSubmitting(false)
    }
  }

  const isMaintenance = liveStatus?.operationalStatus === 'Maintenance'

  return (
    <Modal
      title={
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <SettingOutlined style={{ color: '#0284c7', fontSize: 20 }} />
          <span>Cấu hình Vận hành & Sức chứa Bể bơi</span>
        </div>
      }
      open={open}
      onCancel={onClose}
      onOk={handleSave}
      confirmLoading={submitting}
      okText="Lưu cấu hình"
      cancelText="Đóng"
      width={720}
      destroyOnClose
    >
      {/* 1. Thống kê trực tiếp thời gian thực */}
      <div style={{ marginTop: 8, marginBottom: 16 }}>
        <h4 style={{ margin: '0 0 10px 0', fontSize: 13, color: '#64748b', textTransform: 'uppercase' }}>
          Số liệu trực tiếp hôm nay (Real-time)
        </h4>
        <Row gutter={[12, 12]}>
          <Col xs={12} sm={6}>
            <Card size="small" style={{ background: '#f8fafc', borderRadius: 8 }}>
              <Statistic
                title={<span style={{ fontSize: 12 }}>Đang bơi</span>}
                value={liveStatus?.currentSwimmers ?? 0}
                suffix={<span style={{ fontSize: 12 }}>/ {liveStatus?.maxCapacity ?? 100}</span>}
                valueStyle={{ color: '#0284c7', fontWeight: 700 }}
              />
            </Card>
          </Col>
          <Col xs={12} sm={6}>
            <Card size="small" style={{ background: '#f8fafc', borderRadius: 8 }}>
              <Statistic
                title={<span style={{ fontSize: 12 }}>Tỉ lệ lấp đầy</span>}
                value={liveStatus?.occupancyRate ?? 0}
                suffix="%"
                valueStyle={{ color: liveStatus?.isCrowded ? '#ea580c' : '#16a34a', fontWeight: 700 }}
              />
            </Card>
          </Col>
          <Col xs={12} sm={6}>
            <Card size="small" style={{ background: '#f8fafc', borderRadius: 8 }}>
              <Statistic
                title={<span style={{ fontSize: 12 }}>Vé tại quầy</span>}
                value={liveStatus?.counterTicketsToday ?? 0}
                prefix={<UserOutlined style={{ fontSize: 13 }} />}
                valueStyle={{ fontSize: 18 }}
              />
            </Card>
          </Col>
          <Col xs={12} sm={6}>
            <Card size="small" style={{ background: '#f8fafc', borderRadius: 8 }}>
              <Statistic
                title={<span style={{ fontSize: 12 }}>Vé online</span>}
                value={liveStatus?.onlineTicketsToday ?? 0}
                prefix={<ShoppingOutlined style={{ fontSize: 13 }} />}
                valueStyle={{ fontSize: 18 }}
              />
            </Card>
          </Col>
        </Row>
      </div>

      <Divider style={{ margin: '12px 0' }} />

      {/* 2. Form cấu hình Admin */}
      <Form form={form} layout="vertical" initialValues={{ maxCapacity: 100, warningThresholdPercent: 80 }}>
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="maxCapacity"
              label="Sức chứa tối đa của bể (người)"
              rules={[{ required: true, message: 'Vui lòng nhập sức chứa tối đa' }]}
              extra="Số người bơi tối đa cùng thời điểm."
            >
              <InputNumber min={10} max={1000} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item
              name="warningThresholdPercent"
              label="Ngưỡng cảnh báo đông đúc (%)"
              rules={[{ required: true, message: 'Vui lòng nhập ngưỡng cảnh báo' }]}
              extra="Vượt quá ngưỡng này sẽ cảnh báo khách khi đặt vé."
            >
              <InputNumber min={50} max={100} style={{ width: '100%' }} suffix="%" />
            </Form.Item>
          </Col>
        </Row>

        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="morningRange"
              label="Giờ hoạt động Ca sáng"
              rules={[{ required: true, message: 'Chọn giờ ca sáng' }]}
              extra="Mặc định: 06:00 – 11:30"
            >
              <TimePicker.RangePicker format="HH:mm" style={{ width: '100%' }} minuteStep={15} />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item
              name="afternoonRange"
              label="Giờ hoạt động Ca chiều"
              rules={[{ required: true, message: 'Chọn giờ ca chiều' }]}
              extra="Mặc định: 13:30 – 19:30"
            >
              <TimePicker.RangePicker format="HH:mm" style={{ width: '100%' }} minuteStep={15} />
            </Form.Item>
          </Col>
        </Row>

        <Form.Item
          name="operationalStatus"
          label="Trạng thái vận hành thực tế"
          extra="Khi chuyển sang Bảo trì, hệ thống sẽ hiện Banner đỏ tại trang chủ và chặn đặt vé online."
        >
          <Radio.Group buttonStyle="solid">
            <Radio.Button value="Operating">
              <CheckCircleOutlined style={{ marginRight: 4, color: '#16a34a' }} /> Hoạt động bình thường
            </Radio.Button>
            <Radio.Button value="Maintenance">
              <ExclamationCircleOutlined style={{ marginRight: 4, color: '#dc2626' }} /> Tạm dừng bảo trì thiết bị
            </Radio.Button>
          </Radio.Group>
        </Form.Item>

        <Form.Item
          name="statusReason"
          label="Lý do tạm dừng / Ghi chú bảo dưỡng"
        >
          <Input.TextArea
            rows={2}
            placeholder="VD: Đang bảo dưỡng hệ thống máy bơm lọc nước và bổ sung phao cứu sinh đạt chuẩn..."
            maxLength={300}
            showCount
          />
        </Form.Item>
      </Form>
    </Modal>
  )
}
