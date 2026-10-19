import { useEffect, useRef } from 'react'
import { Alert, Button, Form, Input, Modal, Select } from 'antd'
import { useCreateEquipment } from '../hooks/useCreateEquipment'
import { useEquipmentFilters } from '../hooks/useEquipments'

function jsonValidator(_, value) {
  if (!value?.trim()) return Promise.resolve()
  try {
    const metadata = JSON.parse(value)
    if (metadata && Object.hasOwn(metadata, '_camera')) return Promise.reject(new Error('Không nhập trường hệ thống _camera vào thông số riêng.'))
    return Promise.resolve()
  } catch {
    return Promise.reject(new Error('Thông số riêng phải là JSON hợp lệ.'))
  }
}

function applyServerErrors(form, error) {
  const errors = error?.response?.data?.errors
  if (!errors || typeof errors !== 'object') return
  const fields = Object.entries(errors).flatMap(([key, messages]) => {
    const field = key.split('.').at(-1)
    const cameraFields = { streamurl: 'cameraStreamUrl', protocol: 'cameraProtocol', username: 'cameraUsername', password: 'cameraPassword', cameraconnection: 'cameraStreamUrl' }
    const name = cameraFields[field.toLowerCase()] || field.charAt(0).toLowerCase() + field.slice(1)
    return ['name', 'category', 'location', 'metadata', ...Object.values(cameraFields)].includes(name)
      ? [{ name, errors: Array.isArray(messages) ? messages : [String(messages)] }] : []
  })
  form.setFields(fields)
}

export default function EquipmentFormModal({ open, onClose }) {
  const [form] = Form.useForm()
  const category = Form.useWatch('category', form)
  const isCamera = category?.trim().toLowerCase() === 'camera'
  const submitting = useRef(false)
  const mutation = useCreateEquipment()
  const filters = useEquipmentFilters()
  const { reset } = mutation
  const categories = filters.data?.categories || []

  useEffect(() => {
    if (open) {
      form.resetFields()
      reset()
    }
  }, [open, form, reset])

  function handleFinish(values) {
    if (submitting.current || mutation.isPending || filters.isPending || filters.isError) return
    submitting.current = true
    mutation.reset()
    mutation.mutate({
      name: values.name.trim(), category: values.category.trim(),
      location: values.location?.trim() || null,
      metadata: values.metadata?.trim() ? JSON.parse(values.metadata) : null,
      ...(isCamera ? { cameraConnection: {
        protocol: values.cameraProtocol || 'RTSP', streamUrl: values.cameraStreamUrl.trim(),
        username: values.cameraUsername || null, password: values.cameraPassword || null,
      } } : {}),
    }, {
      onSuccess: () => { form.resetFields(); onClose() },
      onError: error => {
        applyServerErrors(form, error)
        if (isCamera) form.setFieldValue('cameraPassword', undefined)
      },
      onSettled: () => { submitting.current = false },
    })
  }

  function handleCancel() {
    if (submitting.current || mutation.isPending) return
    form.resetFields()
    mutation.reset()
    onClose()
  }

  const errorText = mutation.error?.response?.status === 403
    ? 'Bạn không có quyền thêm thiết bị.'
    : mutation.error?.response?.data?.message || 'Không thể lưu thiết bị. Vui lòng kiểm tra kết nối và thử lại.'

  return <Modal title="Thêm thiết bị" open={open} forceRender width={600}
    onOk={() => form.submit()} onCancel={handleCancel} cancelText="Hủy"
    confirmLoading={mutation.isPending}
    okText={mutation.isPending && isCamera ? 'Đang kiểm tra camera...' : 'Lưu'}
    okButtonProps={{ disabled: filters.isPending || filters.isError }}
    cancelButtonProps={{ disabled: mutation.isPending }} closable={!mutation.isPending}
    keyboard={!mutation.isPending} mask={{ closable: !mutation.isPending }}>
    <Form form={form} layout="vertical" preserve={false} disabled={mutation.isPending} onFinish={handleFinish}
      onValuesChange={() => { if (mutation.isError) mutation.reset() }}>
      {mutation.isError && <Alert type="error" showIcon title="Thêm thiết bị thất bại" description={errorText} className="service-inline-error" />}
      {filters.isError && <Alert type="error" showIcon title="Không thể tải nhóm thiết bị" className="service-inline-error" action={<Button onClick={() => filters.refetch()}>Thử lại</Button>} />}
      {!filters.isPending && !filters.isError && !categories.length && <Alert type="info" showIcon title="Chưa có nhóm thiết bị" description="Nhập tên nhóm đầu tiên. Nhóm sẽ được lưu cùng thiết bị và xuất hiện trong dropdown ở những lần thêm sau." className="service-inline-error" />}
      <Form.Item label="Tên thiết bị" name="name" rules={[
        { required: true, whitespace: true, message: 'Nhập tên thiết bị.' },
        { transform: value => value?.trim(), max: 255, message: 'Tên thiết bị không được vượt quá 255 ký tự.' },
      ]}><Input placeholder="Nhập tên thiết bị" /></Form.Item>
      <Form.Item label="Nhóm thiết bị" name="category" rules={[
        { required: true, whitespace: true, message: categories.length ? 'Chọn nhóm thiết bị.' : 'Nhập nhóm thiết bị đầu tiên.' },
        { transform: value => value?.trim(), max: 50, message: 'Nhóm thiết bị không được vượt quá 50 ký tự.' },
        { validator: (_, value) => !categories.length || !value || categories.includes(value) ? Promise.resolve() : Promise.reject(new Error('Chọn nhóm thiết bị hợp lệ.')) },
      ]}>{categories.length
        ? <Select showSearch optionFilterProp="label" allowClear placeholder="Chọn nhóm thiết bị" loading={filters.isFetching} options={categories.map(value => ({ value, label: value }))} />
        : <Input placeholder="Nhập tên nhóm thiết bị đầu tiên" disabled={filters.isPending || filters.isError || mutation.isPending} />}</Form.Item>
      <Form.Item label="Khu vực/Vị trí" name="location" rules={[
        { transform: value => value?.trim(), max: 255, message: 'Vị trí không được vượt quá 255 ký tự.' },
      ]}><Input placeholder="Nhập vị trí (không bắt buộc)" /></Form.Item>
      {isCamera && <section aria-label="Cấu hình kết nối Camera">
        <h3>Cấu hình kết nối Camera</h3>
        <Alert type="info" showIcon title="Kết nối RTSP qua TCP (H.264/H.265/MJPEG)" description="Chỉ lưu sau khi backend đọc được frame hợp lệ. Địa chỉ và cổng phải được quản trị hệ thống cho phép. Không tự kích hoạt AI." className="service-inline-error" />
        <Form.Item label="Giao thức" name="cameraProtocol" initialValue="RTSP"><Select options={[{ value: 'RTSP', label: 'RTSP (TCP)' }]} /></Form.Item>
        <Form.Item label="RTSP URL" name="cameraStreamUrl" extra="Gồm IP/hostname, cổng (mặc định 554) và đường dẫn stream. Nhập thông tin đăng nhập ở các trường riêng." rules={[
          { required: true, whitespace: true, message: 'Nhập RTSP URL của camera.' },
          { validator: (_, value) => {
            if (!value?.trim()) return Promise.resolve()
            try {
              const uri = new URL(value.trim())
              if (uri.protocol === 'rtsp:' && uri.hostname && uri.port !== '0' && !uri.username && !uri.password && !uri.hash && ![...value].some(character => character.codePointAt(0) <= 31 || character.codePointAt(0) === 127)) return Promise.resolve()
            } catch { /* Display validation below the URL field. */ }
            return Promise.reject(new Error('RTSP URL không hợp lệ hoặc chứa thông tin đăng nhập.'))
          } },
        ]}><Input placeholder="rtsp://<địa chỉ camera>:554/<đường dẫn>" autoComplete="off" /></Form.Item>
        <Form.Item label="Username" name="cameraUsername" dependencies={['cameraPassword']} rules={[
          { validator: (_, value) => !form.getFieldValue('cameraPassword') || value ? Promise.resolve() : Promise.reject(new Error('Nhập username khi sử dụng password.')) },
        ]}><Input autoComplete="off" /></Form.Item>
        <Form.Item label="Password" name="cameraPassword" extra="Không bắt buộc nếu camera không yêu cầu xác thực. Mật khẩu được xóa khỏi form khi kết nối thất bại; hãy nhập lại trước khi thử lưu."><Input.Password visibilityToggle={false} autoComplete="new-password" /></Form.Item>
      </section>}
      <Form.Item label="Thông số riêng (JSON)" name="metadata" extra="Không bắt buộc. Nhập JSON hợp lệ; cấu trúc thông số có thể khác nhau giữa các nhóm." rules={[{ validator: jsonValidator }]}>
        <Input.TextArea rows={5} placeholder="Để trống nếu chưa có thông số" spellCheck={false} />
      </Form.Item>
    </Form>
  </Modal>
}
