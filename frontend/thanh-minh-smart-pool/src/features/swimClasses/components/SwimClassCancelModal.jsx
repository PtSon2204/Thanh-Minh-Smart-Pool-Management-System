import { useState } from 'react'
import { Alert, Form, Input, Modal, message } from 'antd'
import { useCancelClassSession, useDeactivateSwimClass } from '../hooks/useSwimClassManagement'
import { applyClassServerErrors, classServerMessage } from '../utils/classDisplay'

export default function SwimClassCancelModal({ swimClass, session, onClose, onReload }) {
  const [form] = Form.useForm()
  const [error, setError] = useState(null)
  const cancelSession = useCancelClassSession()
  const deactivateClass = useDeactivateSwimClass()
  const isSession = Boolean(session)
  const mutation = isSession ? cancelSession : deactivateClass
  const title = isSession ? 'Hủy buổi học' : 'Ngừng hoạt động lớp'

  async function submit(values) {
    setError(null)
    const payload = {
      reason: values.reason.trim(),
      expectedUpdatedAt: isSession ? session.updatedAt : swimClass.updatedAt,
    }
    try {
      if (isSession) {
        await cancelSession.mutateAsync({ classId: swimClass.id, sessionId: session.id, payload })
      } else {
        await deactivateClass.mutateAsync({ classId: swimClass.id, payload })
      }
      message.success(isSession ? 'Đã hủy buổi học.' : 'Đã ngừng hoạt động lớp.')
      form.resetFields()
      onClose()
    } catch (requestError) {
      applyClassServerErrors(form, requestError)
      setError(classServerMessage(requestError, `Không thể ${isSession ? 'hủy buổi' : 'ngừng lớp'}. Vui lòng thử lại.`))
    }
  }

  return <Modal title={title} open onCancel={onClose} onOk={() => form.submit()}
    okText="Xác nhận" okButtonProps={{ danger: true }} confirmLoading={mutation.isPending}
    okType="primary" cancelText="Quay lại" closable={!mutation.isPending}
    maskClosable={!mutation.isPending} destroyOnHidden>
    <p>{isSession
      ? 'Buổi học sẽ được đánh dấu đã hủy và các học viên liên quan sẽ nhận thông báo.'
      : 'Lớp sẽ ngừng nhận đăng ký; mọi buổi tương lai sẽ bị hủy và các học viên liên quan sẽ nhận thông báo.'}</p>
    {error && <Alert type="error" showIcon title={error}
      action={mutation.error?.response?.status === 409
        ? <a onClick={() => { onClose(); onReload() }}>Tải lại lớp</a> : undefined} />}
    <Form form={form} layout="vertical" onFinish={submit} disabled={mutation.isPending}>
      <Form.Item name="reason" label="Lý do" rules={[
        { required: true, whitespace: true, message: 'Vui lòng nhập lý do.' },
        { max: 500, message: 'Lý do tối đa 500 ký tự.' },
      ]}>
        <Input.TextArea rows={4} maxLength={500} showCount placeholder="Nhập lý do để thông báo cho học viên" />
      </Form.Item>
    </Form>
  </Modal>
}
