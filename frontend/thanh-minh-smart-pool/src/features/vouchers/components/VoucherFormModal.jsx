import { useEffect, useRef } from 'react'
import { Alert, Button, Form, Input, InputNumber, Modal, Select, Switch, DatePicker, Row, Col, Typography, Space } from 'antd'
import { GiftOutlined, TagOutlined, DollarOutlined, PercentageOutlined, CalendarOutlined, CheckCircleOutlined } from '@ant-design/icons'
import { getVoucherErrorMessage, useCreateVoucher } from '../hooks/useVoucherMutations'
import { DiscountType } from '../types/voucher'

const { Text } = Typography

export default function VoucherFormModal({ open, onClose }) {
  const [form] = Form.useForm()
  const submitting = useRef(false)
  const createMutation = useCreateVoucher()
  const isPending = createMutation.isPending
  const mutationError = createMutation.error

  const discountType = Form.useWatch('discountType', form)

  useEffect(() => {
    if (!open) return
    form.resetFields()
    form.setFieldsValue({ 
      discountType: DiscountType.FIXED_AMOUNT, 
      isActive: true, 
      usageLimit: 0 
    })
  }, [form, open])

  const handleFinish = (values) => {
    if (submitting.current || isPending) return
    submitting.current = true

    const payload = {
      code: values.code.trim().toUpperCase(),
      discountType: values.discountType,
      discountValue: values.discountValue,
      usageLimit: values.usageLimit,
      startDate: values.dateRange?.[0]?.toISOString() || null,
      endDate: values.dateRange?.[1]?.toISOString() || null,
      isActive: values.isActive,
    }

    createMutation.reset()
    createMutation.mutate(payload, { 
      onSuccess: onClose, 
      onSettled: () => { submitting.current = false } 
    })
  }

  const handleCancel = () => {
    if (!isPending) {
      createMutation.reset()
      onClose()
    }
  }

  return (
    <Modal
      title={
        <Space align="center">
          <div style={{
            width: 36, height: 36, borderRadius: '8px',
            background: 'linear-gradient(135deg, #36cfc9 0%, #096dd9 100%)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            boxShadow: '0 4px 10px rgba(54, 207, 201, 0.4)'
          }}>
            <GiftOutlined style={{ color: '#fff', fontSize: 20 }} />
          </div>
          <span style={{ fontSize: '20px', fontWeight: 700, color: '#003eb3' }}>Phát hành Voucher mới</span>
        </Space>
      }
      open={open}
      onCancel={handleCancel}
      footer={[
        <Button key="back" onClick={handleCancel} size="large" style={{ borderRadius: '8px' }}>
          Hủy bỏ
        </Button>,
        <Button 
          key="submit" 
          type="primary" 
          loading={isPending} 
          onClick={() => form.submit()} 
          size="large"
          style={{ 
            borderRadius: '8px', 
            background: 'linear-gradient(90deg, #13c2c2 0%, #1890ff 100%)',
            border: 'none',
            boxShadow: '0 4px 12px rgba(19, 194, 194, 0.4)'
          }}
        >
          Tạo mã ngay
        </Button>,
      ]}
      closable={!isPending}
      destroyOnHidden
      width={600}
      styles={{
        header: {
          paddingBottom: '16px',
          borderBottom: '1px solid #e6f7ff',
          marginBottom: '24px'
        },
        body: {
          padding: '0 12px'
        }
      }}
    >
      <Form 
        form={form} 
        layout="vertical" 
        onFinish={handleFinish} 
        preserve={false} 
        initialValues={{ isActive: true, usageLimit: 0, discountType: DiscountType.FIXED_AMOUNT }}
        requiredMark={false}
      >
        {mutationError && (
          <Alert 
            type="error" 
            showIcon 
            message={<Text strong>Không thể tạo voucher</Text>} 
            description={getVoucherErrorMessage(mutationError, 'Hệ thống đang bận, vui lòng thử lại sau.')} 
            action={<Button size="small" danger onClick={() => form.submit()} disabled={isPending}>Thử lại</Button>} 
            style={{ marginBottom: 24, borderRadius: '8px' }}
          />
        )}
        
        <Form.Item 
          label={<Text strong style={{ color: '#096dd9' }}>Mã Voucher</Text>} 
          name="code" 
          rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập mã voucher' }]}
        >
          <Input 
            size="large" 
            placeholder="VD: POOLSALE2026" 
            prefix={<TagOutlined style={{ color: '#69c0ff' }} />}
            style={{ 
              borderRadius: '8px', 
              textTransform: 'uppercase', 
              fontFamily: 'monospace', 
              fontWeight: 'bold',
              color: '#0050b3'
            }} 
          />
        </Form.Item>

        <div style={{ background: '#f0f5ff', padding: '16px 20px', borderRadius: '12px', marginBottom: '24px', border: '1px dashed #adc6ff' }}>
          <Row gutter={24}>
            <Col span={12}>
              <Form.Item 
                label={<Text strong>Loại ưu đãi</Text>} 
                name="discountType" 
                rules={[{ required: true, message: 'Chọn loại ưu đãi' }]}
                style={{ marginBottom: 0 }}
              >
                <Select 
                  size="large"
                  style={{ width: '100%' }}
                  options={[
                    { label: 'Số tiền cố định', value: DiscountType.FIXED_AMOUNT },
                    { label: 'Giảm theo phần trăm', value: DiscountType.PERCENTAGE }
                  ]} 
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item 
                label={<Text strong>Mức giảm</Text>} 
                name="discountValue" 
                rules={[
                  { required: true, message: 'Nhập mức giảm' },
                  { type: 'number', min: 0, message: 'Mức giảm không hợp lệ' }
                ]}
                style={{ marginBottom: 0 }}
              >
                <InputNumber 
                  size="large"
                  min={0} 
                  max={discountType === DiscountType.PERCENTAGE ? 100 : undefined} 
                  style={{ width: '100%', borderRadius: '8px' }} 
                  prefix={discountType === DiscountType.PERCENTAGE ? <PercentageOutlined style={{color: '#722ed1'}}/> : <DollarOutlined style={{color: '#13c2c2'}}/>}
                  formatter={value => `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
                  parser={value => value.replace(/\$\s?|(,*)/g, '')}
                />
              </Form.Item>
            </Col>
          </Row>
        </div>

        <Row gutter={24}>
          <Col span={12}>
            <Form.Item 
              label={<Text strong style={{ color: '#096dd9' }}>Thời hạn sử dụng</Text>} 
              name="dateRange"
            >
              <DatePicker.RangePicker 
                size="large"
                showTime 
                format="DD/MM/YY HH:mm"
                style={{ width: '100%', borderRadius: '8px' }}
                suffixIcon={<CalendarOutlined style={{ color: '#1890ff' }} />}
              />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item 
              label={<Text strong style={{ color: '#096dd9' }}>Lượt dùng tối đa</Text>} 
              name="usageLimit"
              rules={[{ type: 'number', min: 0, message: 'Không hợp lệ' }]}
              extra={<Text type="secondary" style={{ fontSize: '12px' }}>*Nhập 0 nếu không giới hạn</Text>}
            >
              <InputNumber size="large" min={0} style={{ width: '100%', borderRadius: '8px' }} />
            </Form.Item>
          </Col>
        </Row>

        <Form.Item 
          name="isActive" 
          valuePropName="checked"
          style={{ marginBottom: 0, marginTop: 12, padding: '12px 16px', background: '#e6fffb', borderRadius: '8px', border: '1px solid #87e8de' }}
        >
          <Switch 
            checkedChildren={<><CheckCircleOutlined /> Kích hoạt ngay</>} 
            unCheckedChildren="Chưa kích hoạt" 
            defaultChecked
            style={{ width: '100%', background: form.getFieldValue('isActive') ? '#13c2c2' : undefined }}
          />
        </Form.Item>
      </Form>
    </Modal>
  )
}
