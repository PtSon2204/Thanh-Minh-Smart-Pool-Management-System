import { useEffect } from 'react'
import dayjs from 'dayjs'
import { Col, Form, Input, Modal, Row, Select } from 'antd'
import { USER_ROLES } from '../userManagement'

const phonePattern = /^(?:0|\+84)[35789]\d{8}$/
const usernamePattern = /^[A-Za-z0-9](?:[A-Za-z0-9._]*[A-Za-z0-9])?$/

export default function UserFormModal({ open, record, saving, onClose, onSave }) {
  const [form] = Form.useForm()

  useEffect(() => {
    if (!open) return
    form.resetFields()
    form.setFieldsValue(record
      ? {
          username: record.username,
          email: record.email,
          phone: record.phone,
          fullName: record.fullName,
          dateOfBirth: record.dateOfBirth,
          address: record.address,
          role: record.role,
        }
      : { role: 'CUSTOMER' })
  }, [form, open, record])

  return <Modal
    title={record ? 'Chỉnh sửa tài khoản' : 'Thêm tài khoản'}
    open={open}
    onCancel={onClose}
    onOk={() => form.submit()}
    okText={record ? 'Lưu thay đổi' : 'Tạo tài khoản'}
    cancelText="Hủy"
    confirmLoading={saving}
    destroyOnHidden
  >
    <Form form={form} layout="vertical" className="um-form" onFinish={onSave}>
      <Form.Item name="fullName" label="Họ và tên" rules={[{ required: true, whitespace: true, message: 'Nhập họ và tên.' }]}>
        <Input maxLength={255} />
      </Form.Item>
      <Form.Item name="username" label="Tên đăng nhập" rules={[
        { required: true, message: 'Nhập tên đăng nhập.' },
        { min: 3, message: 'Tối thiểu 3 ký tự.' },
        { pattern: usernamePattern, message: 'Chỉ dùng chữ, số, dấu chấm hoặc gạch dưới; bắt đầu và kết thúc bằng chữ hoặc số.' },
      ]}>
        <Input maxLength={100} disabled={Boolean(record)} />
      </Form.Item>
      <Row gutter={12}>
        <Col span={12}>
          <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email', message: 'Nhập email hợp lệ.' }]}>
            <Input maxLength={150} />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name="phone" label="Số điện thoại" rules={[
            { required: true, message: 'Nhập số điện thoại.' },
            { pattern: phonePattern, message: 'Số di động Việt Nam không hợp lệ.' },
          ]}>
            <Input />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={12}>
        <Col span={12}>
          <Form.Item name="dateOfBirth" label="Ngày sinh" rules={[{
            validator: (_, value) => !value || (
              dayjs(value).format('YYYY-MM-DD') === value &&
              value <= dayjs().format('YYYY-MM-DD')
            ) ? Promise.resolve() : Promise.reject(new Error('Ngày sinh không hợp lệ.')),
          }]}>
            <Input type="date" max={dayjs().format('YYYY-MM-DD')} />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name="role" label="Vai trò" rules={[{ required: true, message: 'Chọn vai trò.' }]}>
            <Select options={USER_ROLES} />
          </Form.Item>
        </Col>
      </Row>
      <Form.Item name="address" label="Địa chỉ">
        <Input.TextArea rows={2} maxLength={500} />
      </Form.Item>
      {!record && <Row gutter={12}>
        <Col span={12}>
          <Form.Item name="password" label="Mật khẩu" rules={[
            { required: true, message: 'Nhập mật khẩu.' },
            { min: 8, message: 'Tối thiểu 8 ký tự.' },
            { pattern: /^(?=.*[A-Za-z])(?=.*\d).+$/, message: 'Cần ít nhất một chữ cái và một chữ số.' },
          ]}>
            <Input.Password maxLength={128} />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name="confirmPassword" label="Xác nhận mật khẩu" dependencies={['password']} rules={[
            { required: true, message: 'Xác nhận mật khẩu.' },
            ({ getFieldValue }) => ({
              validator: (_, value) => value === getFieldValue('password')
                ? Promise.resolve()
                : Promise.reject(new Error('Mật khẩu xác nhận không khớp.')),
            }),
          ]}>
            <Input.Password maxLength={128} />
          </Form.Item>
        </Col>
      </Row>}
    </Form>
  </Modal>
}
