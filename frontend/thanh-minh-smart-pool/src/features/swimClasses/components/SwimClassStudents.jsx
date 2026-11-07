import { useState } from 'react'
import { Alert, Button, Empty, Form, Input, Modal, Pagination, Segmented, Spin, Tag, message } from 'antd'
import { ReloadOutlined, StopOutlined, TeamOutlined } from '@ant-design/icons'
import { useClassRegistrations, useCancelClassRegistration } from '../hooks/useClassRegistration'
import { formatVietnamTime } from '../utils/classSchedule'
import { applyClassServerErrors, classServerMessage } from '../utils/classDisplay'

const filters = [
  { label: 'Tất cả', value: 'all' },
  { label: 'Đang học', value: 'Confirmed' },
  { label: 'Đã hủy', value: 'Cancelled' },
]

export default function SwimClassStudents({ classId }) {
  const [status, setStatus] = useState('all')
  const [pageIndex, setPageIndex] = useState(1)
  const [selected, setSelected] = useState(null)
  const [submitError, setSubmitError] = useState(null)
  const [form] = Form.useForm()
  const params = { pageIndex, pageSize: 10, ...(status !== 'all' ? { status } : {}) }
  const registrations = useClassRegistrations(classId, params, true)
  const cancel = useCancelClassRegistration()
  const items = registrations.data?.items || []

  function closeModal() {
    if (cancel.isPending) return
    setSelected(null)
    setSubmitError(null)
    form.resetFields()
  }

  async function submit(values) {
    if (!selected) return
    setSubmitError(null)
    try {
      await cancel.mutateAsync({
        classId,
        registrationId: selected.registrationId,
        payload: { reason: values.reason.trim(), expectedUpdatedAt: selected.updatedAt },
      })
      message.success('Đã hủy đăng ký của học viên.')
      closeModal()
      if (status === 'Confirmed' && items.length === 1 && pageIndex > 1) {
        setPageIndex(pageIndex - 1)
      }
    } catch (error) {
      applyClassServerErrors(form, error)
      setSubmitError(classServerMessage(error, 'Không thể hủy đăng ký. Vui lòng thử lại.'))
    }
  }

  return <section className="swim-class-detail-panel swim-class-students" aria-label="Danh sách học viên">
    <div className="swim-class-detail-section-title"><TeamOutlined /><h2>Học viên đăng ký</h2>
      <span>{registrations.data?.totalCount ?? 0} đăng ký</span></div>
    <div className="swim-class-students-toolbar">
      <Segmented options={filters} value={status} onChange={value => { setStatus(value); setPageIndex(1) }}
        aria-label="Lọc trạng thái đăng ký" />
      <Button type="text" icon={<ReloadOutlined spin={registrations.isFetching} />}
        onClick={() => registrations.refetch()} disabled={registrations.isFetching}>Làm mới</Button>
    </div>

    {registrations.isPending && <div className="swim-class-students-loading"><Spin /> Đang tải học viên...</div>}
    {registrations.isError && <Alert type="error" showIcon title="Không tải được danh sách học viên"
      action={<Button onClick={() => registrations.refetch()}>Thử lại</Button>} />}
    {!registrations.isPending && !registrations.isError && items.length === 0 &&
      <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có đăng ký ở trạng thái này." />}
    {!registrations.isPending && !registrations.isError && items.length > 0 && <>
      <div className="swim-class-students-list">
        {items.map(item => <article className="swim-class-student" key={item.registrationId}>
          <div className="swim-class-student-main">
            <strong>{item.customerName || 'Học viên'}</strong>
            <span>Đăng ký {formatVietnamTime(item.registeredAt)}</span>
            {item.status === 'Cancelled' && item.cancelReason &&
              <span className="swim-class-student-reason">Lý do hủy: {item.cancelReason}</span>}
          </div>
          <Tag color={item.status === 'Confirmed' ? 'success' : 'default'}>
            {item.status === 'Confirmed' ? 'Đang học' : 'Đã hủy'}
          </Tag>
          {item.status === 'Confirmed' && <Button danger icon={<StopOutlined />}
            onClick={() => { setSelected(item); setSubmitError(null) }}>Hủy đăng ký</Button>}
        </article>)}
      </div>
      <Pagination className="swim-class-students-pagination" current={pageIndex} pageSize={params.pageSize}
        total={registrations.data?.totalCount || 0} showSizeChanger={false} onChange={setPageIndex} />
    </>}

    <Modal title="Hủy đăng ký của học viên" open={Boolean(selected)} onCancel={closeModal}
      onOk={() => form.submit()} okText="Xác nhận hủy" okType="danger"
      cancelText="Quay lại" confirmLoading={cancel.isPending}
      closable={!cancel.isPending} maskClosable={!cancel.isPending} destroyOnHidden>
      <p>Học viên {selected?.customerName} sẽ không còn giữ chỗ trong lớp và sẽ nhận thông báo.</p>
      {submitError && <Alert type="error" showIcon title={submitError}
        action={cancel.error?.response?.status === 409
          ? <Button type="link" onClick={() => { closeModal(); registrations.refetch() }}>Tải lại danh sách</Button>
          : undefined} />}
      <Form form={form} layout="vertical" onFinish={submit} disabled={cancel.isPending}>
        <Form.Item name="reason" label="Lý do hủy" rules={[
          { required: true, whitespace: true, message: 'Vui lòng nhập lý do hủy.' },
          { max: 500, message: 'Lý do tối đa 500 ký tự.' },
        ]}>
          <Input.TextArea rows={4} maxLength={500} showCount placeholder="Nhập lý do để thông báo cho học viên" />
        </Form.Item>
      </Form>
    </Modal>
  </section>
}
