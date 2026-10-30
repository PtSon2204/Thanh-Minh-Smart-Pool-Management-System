import { useEffect, useRef, useMemo } from 'react'
import { Alert, Button, Form, Input, InputNumber, Modal, Select, DatePicker, Row, Col, Typography, Space, Tooltip } from 'antd'
import {
  GiftOutlined,
  EditOutlined,
  TagOutlined,
  DollarOutlined,
  PercentageOutlined,
  CalendarOutlined,
  ClockCircleOutlined
} from '@ant-design/icons'
import { getVoucherErrorMessage, useCreateVoucher, useUpdateVoucher } from '../hooks/useVoucherMutations'
import { DiscountType } from '../types/voucher'
import dayjs from 'dayjs'

const { Text } = Typography

export default function VoucherFormModal({ open, onClose, editingVoucher = null }) {
  const [form] = Form.useForm()
  const submitting = useRef(false)

  const isEdit = editingVoucher !== null
  const createMutation = useCreateVoucher()
  const updateMutation = useUpdateVoucher()
  const activeMutation = isEdit ? updateMutation : createMutation

  const isPending = activeMutation.isPending
  const mutationError = activeMutation.error

  const discountType = Form.useWatch('discountType', form)
  const watchStartDate = Form.useWatch('startDate', form)
  const watchEndDate = Form.useWatch('endDate', form)

  // Hàm tính toán giá trị khởi tạo cho form
  const getInitialValues = () => {
    if (editingVoucher) {
      return {
        code: editingVoucher.code || '',
        discountType: editingVoucher.discountType || DiscountType.FIXED_AMOUNT,
        discountValue: editingVoucher.discountValue ?? undefined,
        minOrderValue: editingVoucher.minOrderValue ?? undefined,
        startDate: editingVoucher.startDate ? dayjs(editingVoucher.startDate) : null,
        endDate: editingVoucher.endDate ? dayjs(editingVoucher.endDate) : null,
        isActive: editingVoucher.isActive ?? true,
      }
    }
    return {
      discountType: DiscountType.FIXED_AMOUNT,
      startDate: dayjs().startOf('day'),
      endDate: dayjs().add(30, 'day').endOf('day'),
      isActive: true,
    }
  }

  useEffect(() => {
    if (!open) return
    activeMutation.reset()

    if (isEdit && editingVoucher) {
      form.setFieldsValue({
        code: editingVoucher.code || '',
        discountType: editingVoucher.discountType || DiscountType.FIXED_AMOUNT,
        discountValue: editingVoucher.discountValue ?? undefined,
        minOrderValue: editingVoucher.minOrderValue ?? undefined,
        startDate: editingVoucher.startDate ? dayjs(editingVoucher.startDate) : null,
        endDate: editingVoucher.endDate ? dayjs(editingVoucher.endDate) : null,
        isActive: editingVoucher.isActive ?? true,
      })
    } else if (!isEdit) {
      form.resetFields()
      form.setFieldsValue({
        discountType: DiscountType.FIXED_AMOUNT,
        startDate: dayjs().startOf('day'),
        endDate: dayjs().add(30, 'day').endOf('day'),
        isActive: true,
      })
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, editingVoucher, isEdit, form])

  // Xử lý nút chọn nhanh thời hạn
  const handleQuickDuration = (days) => {
    const currentStart = form.getFieldValue('startDate') || dayjs()
    const newEnd = currentStart.add(days, 'day').hour(23).minute(59).second(59)
    form.setFieldsValue({ endDate: newEnd })
  }

  const handleEndOfMonth = () => {
    const currentStart = form.getFieldValue('startDate') || dayjs()
    const newEnd = currentStart.endOf('month').hour(23).minute(59).second(59)
    form.setFieldsValue({ endDate: newEnd })
  }

  const handleClearDuration = () => {
    form.setFieldsValue({ startDate: null, endDate: null })
  }

  // Khóa các ngày quá khứ
  const disabledStartDate = (current) => {
    if (!current) return false
    // Nếu đang chỉnh sửa và ngày hiển thị trùng với startDate cũ của voucher thì vẫn cho phép hiển thị
    if (isEdit && editingVoucher?.startDate && current.isSame(dayjs(editingVoucher.startDate), 'day')) {
      return false
    }
    return current < dayjs().startOf('day')
  }

  const disabledEndDate = (current) => {
    if (!current) return false
    const currentStart = form.getFieldValue('startDate')
    const minDate = currentStart && currentStart.isAfter(dayjs().startOf('day'))
      ? currentStart.startOf('day')
      : dayjs().startOf('day')
    return current < minDate
  }

  // Tóm tắt thời hạn trực quan
  const durationSummary = useMemo(() => {
    if (!watchStartDate && !watchEndDate) {
      return { text: 'Vô thời hạn (áp dụng liên tục)', isWarning: false }
    }
    if (watchStartDate && watchEndDate) {
      if (watchEndDate.isBefore(watchStartDate)) {
        return { text: 'Ngày kết thúc phải sau ngày bắt đầu!', isWarning: true }
      }
      const days = Math.ceil(watchEndDate.diff(watchStartDate, 'day', true))
      return {
        text: `Áp dụng trong ~${days} ngày (từ ${watchStartDate.format('DD/MM/YYYY HH:mm')} đến ${watchEndDate.format('DD/MM/YYYY HH:mm')})`,
        isWarning: false
      }
    }
    if (watchStartDate) {
      return { text: `Bắt đầu từ ${watchStartDate.format('DD/MM/YYYY HH:mm')} (không giới hạn kết thúc)`, isWarning: false }
    }
    return { text: `Hạn chót đến ${watchEndDate.format('DD/MM/YYYY HH:mm')}`, isWarning: false }
  }, [watchStartDate, watchEndDate])

  const handleFinish = (values) => {
    if (submitting.current || isPending) return
    submitting.current = true

    let start = values.startDate ? dayjs(values.startDate) : null
    let end = values.endDate ? dayjs(values.endDate) : null

    // Nếu người dùng chọn ngày kết thúc mà chưa chỉnh giờ (00:00:00),
    // tự động chuyển thành 23:59:59 để voucher có hiệu lực trọn vẹn cả ngày đó
    if (end && end.hour() === 0 && end.minute() === 0 && end.second() === 0) {
      end = end.hour(23).minute(59).second(59)
    }

    const payload = {
      code: (values.code || editingVoucher?.code || '').trim().toUpperCase(),
      discountType: values.discountType || editingVoucher?.discountType,
      discountValue: values.discountValue !== undefined ? values.discountValue : editingVoucher?.discountValue,
      minOrderValue: values.minOrderValue ?? null,
      startDate: start ? start.toISOString() : null,
      endDate: end ? end.toISOString() : null,
      isActive: isEdit ? (editingVoucher?.isActive ?? true) : true,
    }

    if (isEdit) {
      updateMutation.mutate(
        { id: editingVoucher.id, data: payload },
        { onSuccess: onClose, onSettled: () => { submitting.current = false } }
      )
    } else {
      createMutation.mutate(payload, {
        onSuccess: onClose,
        onSettled: () => { submitting.current = false }
      })
    }
  }

  const handleCancel = () => {
    if (!isPending) {
      activeMutation.reset()
      onClose()
    }
  }

  return (
    <Modal
      title={
        <Space align="center">
          <div style={{
            width: 36, height: 36, borderRadius: '8px',
            background: isEdit
              ? 'linear-gradient(135deg, #fa8c16 0%, #d46b08 100%)'
              : 'linear-gradient(135deg, #36cfc9 0%, #096dd9 100%)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            boxShadow: isEdit
              ? '0 4px 10px rgba(250, 140, 22, 0.4)'
              : '0 4px 10px rgba(54, 207, 201, 0.4)'
          }}>
            {isEdit
              ? <EditOutlined style={{ color: '#fff', fontSize: 20 }} />
              : <GiftOutlined style={{ color: '#fff', fontSize: 20 }} />
            }
          </div>
          <span style={{ fontSize: '18px', fontWeight: 700, color: '#003eb3' }}>
            {isEdit ? 'Chỉnh sửa Voucher' : 'Phát hành Voucher mới'}
          </span>
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
            background: isEdit
              ? 'linear-gradient(90deg, #fa8c16 0%, #d46b08 100%)'
              : 'linear-gradient(90deg, #13c2c2 0%, #1890ff 100%)',
            border: 'none',
            boxShadow: isEdit
              ? '0 4px 12px rgba(250, 140, 22, 0.4)'
              : '0 4px 12px rgba(19, 194, 194, 0.4)'
          }}
        >
          {isEdit ? 'Lưu thay đổi' : 'Tạo mã ngay'}
        </Button>,
      ]}
      closable={!isPending}
      destroyOnClose
      width={640}
      styles={{
        header: { paddingBottom: '16px', borderBottom: '1px solid #e6f7ff', marginBottom: '20px' },
        body: { padding: '0 12px' }
      }}
    >
      <Form
        key={editingVoucher?.id || 'create'}
        form={form}
        layout="vertical"
        initialValues={getInitialValues()}
        onFinish={handleFinish}
        requiredMark={false}
      >
        {mutationError && (
          <Alert
            type="error"
            showIcon
            message={<Text strong>{isEdit ? 'Không thể cập nhật voucher' : 'Không thể tạo voucher'}</Text>}
            description={getVoucherErrorMessage(mutationError, 'Hệ thống đang bận, vui lòng thử lại sau.')}
            action={<Button size="small" danger onClick={() => form.submit()} disabled={isPending}>Thử lại</Button>}
            style={{ marginBottom: 20, borderRadius: '8px' }}
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
            disabled={isEdit}
            style={{
              borderRadius: '8px',
              textTransform: 'uppercase',
              fontFamily: 'monospace',
              fontWeight: 'bold',
              color: isEdit ? '#595959' : '#0050b3',
              backgroundColor: isEdit ? '#f5f5f5' : '#fff'
            }}
          />
        </Form.Item>

        <div style={{
          background: '#f0f5ff', padding: '16px 20px', borderRadius: '12px',
          marginBottom: '20px', border: '1px dashed #adc6ff'
        }}>
          <Row gutter={24}>
            <Col xs={24} sm={12}>
              <Form.Item
                label={<Text strong>Loại ưu đãi</Text>}
                name="discountType"
                rules={[{ required: true, message: 'Chọn loại ưu đãi' }]}
                style={{ marginBottom: 0 }}
              >
                <Select
                  size="large"
                  disabled={isEdit}
                  style={{ width: '100%' }}
                  options={[
                    { label: 'Số tiền cố định', value: DiscountType.FIXED_AMOUNT },
                    { label: 'Giảm theo phần trăm', value: DiscountType.PERCENTAGE }
                  ]}
                />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
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
                  disabled={isEdit}
                  max={discountType === DiscountType.PERCENTAGE ? 100 : undefined}
                  style={{ width: '100%', borderRadius: '8px' }}
                  prefix={discountType === DiscountType.PERCENTAGE
                    ? <PercentageOutlined style={{ color: '#722ed1' }} />
                    : <DollarOutlined style={{ color: '#13c2c2' }} />}
                  formatter={value => value !== undefined && value !== null ? `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',') : ''}
                  parser={value => value.replace(/,*/g, '')}
                />
              </Form.Item>
            </Col>
          </Row>
        </div>

        {/* KHU VỰC CHỌN THỜI HẠN SỬ DỤNG - THIẾT KẾ MỚI TRỰC QUAN & DỄ CHỌN */}
        <div style={{
          background: '#fafafa',
          border: '1px solid #d9d9d9',
          borderRadius: '12px',
          padding: '16px',
          marginBottom: '20px'
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, flexWrap: 'wrap', gap: 8 }}>
            <Text strong style={{ color: '#096dd9', fontSize: '13.5px' }}>
              <CalendarOutlined style={{ marginRight: 6 }} />
              Thời gian áp dụng
            </Text>
            {/* Phím chọn nhanh thời hạn */}
            <Space size={6} wrap>
              <Text type="secondary" style={{ fontSize: '12px' }}>Chọn nhanh:</Text>
              <Button size="small" style={{ borderRadius: '6px', fontSize: '12px' }} onClick={() => handleQuickDuration(7)}>+7 ngày</Button>
              <Button size="small" style={{ borderRadius: '6px', fontSize: '12px' }} onClick={() => handleQuickDuration(30)}>+30 ngày</Button>
              <Button size="small" style={{ borderRadius: '6px', fontSize: '12px' }} onClick={() => handleQuickDuration(90)}>+90 ngày</Button>
              <Button size="small" style={{ borderRadius: '6px', fontSize: '12px' }} onClick={handleEndOfMonth}>Hết tháng</Button>
              <Tooltip title="Voucher áp dụng mãi mãi">
                <Button size="small" danger type="dashed" style={{ fontSize: '12px' }} onClick={handleClearDuration}>Vô hạn</Button>
              </Tooltip>
            </Space>
          </div>

          <Row gutter={16}>
            <Col xs={24} sm={12}>
              <Form.Item
                label={<Text strong style={{ fontSize: '13px', color: '#595959' }}>Ngày bắt đầu</Text>}
                name="startDate"
                style={{ marginBottom: 6 }}
                rules={[
                  {
                    validator: (_, value) => {
                      if (!value) return Promise.resolve()
                      if (!isEdit && value.isBefore(dayjs().startOf('day'))) {
                        return Promise.reject(new Error('Ngày bắt đầu không được ở quá khứ'))
                      }
                      return Promise.resolve()
                    }
                  }
                ]}
              >
                <DatePicker
                  size="large"
                  showTime={{ format: 'HH:mm' }}
                  format="DD/MM/YYYY HH:mm"
                  disabledDate={disabledStartDate}
                  placeholder="Từ ngày (mặc định: hôm nay)"
                  style={{ width: '100%', borderRadius: '8px' }}
                />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item
                label={<Text strong style={{ fontSize: '13px', color: '#595959' }}>Ngày kết thúc</Text>}
                name="endDate"
                style={{ marginBottom: 6 }}
                dependencies={['startDate']}
                rules={[
                  ({ getFieldValue }) => ({
                    validator(_, value) {
                      if (!value) return Promise.resolve()
                      if (value.isBefore(dayjs().startOf('day'))) {
                        return Promise.reject(new Error('Ngày kết thúc phải là hôm nay hoặc tương lai'))
                      }
                      const start = getFieldValue('startDate')
                      if (start && value.isBefore(start)) {
                        return Promise.reject(new Error('Ngày kết thúc phải sau ngày bắt đầu'))
                      }
                      return Promise.resolve()
                    }
                  })
                ]}
              >
                <DatePicker
                  size="large"
                  showTime={{
                    format: 'HH:mm',
                    defaultValue: dayjs('23:59:59', 'HH:mm:ss')
                  }}
                  format="DD/MM/YYYY HH:mm"
                  disabledDate={disabledEndDate}
                  placeholder="Đến ngày (để trống nếu vô hạn)"
                  style={{ width: '100%', borderRadius: '8px' }}
                />
              </Form.Item>
            </Col>
          </Row>

          <div style={{
            marginTop: 8,
            fontSize: '12.5px',
            color: durationSummary.isWarning ? '#cf1322' : '#389e0d',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            fontWeight: 500
          }}>
            <ClockCircleOutlined />
            <span>{durationSummary.text}</span>
          </div>
        </div>

        <Form.Item
          label={<Text strong style={{ color: '#096dd9' }}>Đơn tối thiểu (VNĐ)</Text>}
          name="minOrderValue"
          rules={[{ type: 'number', min: 0, message: 'Không hợp lệ' }]}
          extra={<Text type="secondary" style={{ fontSize: '12px' }}>*Để trống nếu không yêu cầu giá trị tối thiểu</Text>}
          style={{ marginBottom: 0 }}
        >
          <InputNumber
            size="large"
            min={0}
            style={{ width: '100%', borderRadius: '8px' }}
            formatter={value => value ? `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',') : ''}
            parser={value => value.replace(/,*/g, '')}
            placeholder="VD: 100,000"
          />
        </Form.Item>
      </Form>
    </Modal>
  )
}
