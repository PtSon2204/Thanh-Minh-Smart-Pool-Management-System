import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Button, Card, Col, Form, InputNumber, Popconfirm, Row, Space, Spin, Tag, Typography, message,
} from 'antd'
import { ReloadOutlined, SaveOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { useAuthStore } from '../../../features/auth/store/authStore'
import { monitoringService } from '../../../features/safety/services/monitoringService'
import './Monitoring.css'

const { Title, Text } = Typography

const FIELDS = [
  { name: 'confidenceThreshold', label: 'Độ tin cậy phát hiện', min: 0, max: 1, step: 0.01, hint: 'Từ 0 đến 1. Dấu dưới ngưỡng bị bỏ qua.' },
  { name: 'warningRatio', label: 'Tỷ lệ cảnh báo', min: 0, max: 1, step: 0.05, hint: 'Phần frame nguy hiểm trong cửa sổ để phát cảnh báo (phải < tỷ lệ khẩn cấp).' },
  { name: 'criticalRatio', label: 'Tỷ lệ khẩn cấp', min: 0, max: 1, step: 0.05, hint: 'Phần frame nguy hiểm để nâng lên khẩn cấp (≥ tỷ lệ cảnh báo).' },
  { name: 'recoveryRatio', label: 'Tỷ lệ hồi phục', min: 0, max: 1, step: 0.05, hint: 'Phần frame an toàn để kết thúc cảnh báo (phải < tỷ lệ cảnh báo).' },
  { name: 'criticalHoldSeconds', label: 'Giữ khẩn cấp (giây)', min: 0.1, step: 0.5, hint: 'Thời gian duy trì liên tục trước khi nâng lên khẩn cấp.' },
  { name: 'recoveryHoldSeconds', label: 'Giữ hồi phục (giây)', min: 0.1, step: 0.5, hint: 'Thời gian duy trì an toàn liên tục trước khi kết thúc cảnh báo.' },
  { name: 'cooldownSeconds', label: 'Khoảng cooldown (giây)', min: 0, step: 1, hint: 'Khoảng chờ sau hồi phục trước khi được phát cảnh báo mới cùng mức.' },
  { name: 'windowSeconds', label: 'Cửa sổ quan sát (giây)', min: 1, step: 1, hint: 'Khoảng thời gian lấy mẫu để tính tỷ lệ an toàn/nguy hiểm.' },
  { name: 'maxObservationGapSeconds', label: 'Khoảng trống tối đa (giây)', min: 0.1, step: 0.5, hint: 'Mất tín hiệu camera lâu hơn sẽ làm reset cửa sổ theo dõi.' },
  { name: 'minObservations', label: 'Số quan sát tối thiểu', min: 2, step: 1, hint: 'Cần đủ mẫu trong cửa sổ trước khi phân loại.' },
]

export default function ThresholdsPage() {
  const isAdmin = useAuthStore(state => state.session?.role?.toUpperCase() === 'ADMIN')
  const [form] = Form.useForm()
  const queryClient = useQueryClient()
  const [saving, setSaving] = useState(false)

  const { data, isFetching, isLoading } = useQuery({
    queryKey: ['safety', 'thresholds'],
    queryFn: ({ signal }) => monitoringService.thresholds.get(signal),
    refetchInterval: 60000,
  })

  const save = useMutation({
    mutationFn: payload => monitoringService.thresholds.save(payload),
    onSuccess: (next) => {
      queryClient.setQueryData(['safety', 'thresholds'], next)
      message.success(`Đã lưu bộ ngưỡng (phiên bản ${next.version}). Thay đổi có hiệu lực ngay trên các camera đang phân tích.`)
      setSaving(false)
    },
    onError: (err) => {
      message.error(err?.response?.data?.message || 'Không lưu được bộ ngưỡng.')
      setSaving(false)
    },
  })

  // Đổ dữ liệu lần đầu khi load xong (không đè khi người dùng đang chỉnh).
  useEffect(() => {
    if (data) {
      form.setFieldsValue({
        confidenceThreshold: data.confidenceThreshold,
        warningRatio: data.warningRatio,
        criticalRatio: data.criticalRatio,
        recoveryRatio: data.recoveryRatio,
        criticalHoldSeconds: data.criticalHoldSeconds,
        recoveryHoldSeconds: data.recoveryHoldSeconds,
        cooldownSeconds: data.cooldownSeconds,
        windowSeconds: data.windowSeconds,
        maxObservationGapSeconds: data.maxObservationGapSeconds,
        minObservations: data.minObservations,
      })
    }
  }, [data, form])

  const handleSave = async () => {
    try {
      const values = await form.validateFields()
      setSaving(true)
      save.mutate(values)
    } catch {
      // antd validateFields throw khi sai — message lỗi đã hiện tại từng ô.
    }
  }

  const handleReset = () => {
    form.resetFields()
    if (data) {
      form.setFieldsValue({
        confidenceThreshold: data.confidenceThreshold,
        warningRatio: data.warningRatio,
        criticalRatio: data.criticalRatio,
        recoveryRatio: data.recoveryRatio,
        criticalHoldSeconds: data.criticalHoldSeconds,
        recoveryHoldSeconds: data.recoveryHoldSeconds,
        cooldownSeconds: data.cooldownSeconds,
        windowSeconds: data.windowSeconds,
        maxObservationGapSeconds: data.maxObservationGapSeconds,
        minObservations: data.minObservations,
      })
    }
  }

  return (
    <div className="monitoring-page safety-thresholds">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 16, flexWrap: 'wrap' }}>
        <div>
          <Title level={4} style={{ margin: 0 }}>Ngưỡng cảnh báo AI</Title>
          <Text type="secondary">
            Bộ ngưỡng điều khiển khi nào AI phát cảnh báo / khẩn cấp / hồi phục.
            Sửa chỉ dành cho Admin; thay đổi có hiệu lực ngay trên các camera đang chạy
            (không làm mất trạng thái theo dõi).
          </Text>
        </div>
        {data && (
          <Space size={8} wrap>
            <Tag color={data.origin === 'DATABASE' ? 'blue' : 'orange'}>
              Nguồn: {data.origin === 'DATABASE' ? 'Cấu hình (lưu DB)' : 'Mặc định appsettings'}
            </Tag>
            <Tag>Phiên bản: {data.version || '—'}</Tag>
            {data.updatedAt && <Text type="secondary">Cập nhật {dayjs(data.updatedAt).format('DD/MM HH:mm')}</Text>}
          </Space>
        )}
      </div>

      {!isAdmin && (
        <Alert
          type="info"
          showIcon
          message="Bạn chỉ xem được bộ ngưỡng. Chỉ Admin mới có quyền sửa."
        />
      )}

      <Spin spinning={isLoading || isFetching}>
        <Card>
          <Form form={form} layout="vertical" disabled={!isAdmin}>
            <Row gutter={[16, 16]}>
              {FIELDS.map(f => (
                <Col key={f.name} xs={24} sm={12} xl={8}>
                  <Form.Item
                    name={f.name}
                    label={f.label}
                    tooltip={f.hint}
                    rules={[{ required: true, message: `Thiếu ${f.label.toLowerCase()}.` }]}
                  >
                    <InputNumber
                      style={{ width: '100%' }}
                      min={f.min}
                      max={f.max}
                      step={f.step}
                      precision={f.name === 'minObservations' ? 0 : 2}
                    />
                  </Form.Item>
                </Col>
              ))}
            </Row>
          </Form>

          <div className="safety-form-actions">
            <Button icon={<ReloadOutlined />} onClick={handleReset} disabled={!isAdmin}>
              Reset
            </Button>
            <Popconfirm
              title="Lưu bộ ngưỡng mới?"
              description="Thay đổi sẽ áp dụng ngay trên các camera đang phân tích."
              onConfirm={handleSave}
              okText="Lưu"
              cancelText="Hủy"
              disabled={!isAdmin}
            >
              <Button type="primary" icon={<SaveOutlined />} loading={saving} disabled={!isAdmin}>
                Lưu thay đổi
              </Button>
            </Popconfirm>
          </div>
        </Card>
      </Spin>
    </div>
  )
}
