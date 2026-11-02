import { useEffect, useRef, useState } from 'react'
import { Alert, Button, Modal, Space, Tag, Typography, Upload } from 'antd'
import { EyeOutlined, UploadOutlined } from '@ant-design/icons'
import { documentLabels, documentStatusLabels, formatOnboardingDate, onboardingError } from '../onboarding/onboardingUi'

export default function OnboardingDocuments({ documents = [], editable = false, disabled = false, onUpload, onRead }) {
  const [preview, setPreview] = useState(null)
  const [error, setError] = useState('')
  const [uploading, setUploading] = useState(null)
  const previewUrl = useRef(null)
  const request = useRef(null)
  const uploadGuard = useRef(false)
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
      request.current?.abort()
      if (previewUrl.current) URL.revokeObjectURL(previewUrl.current)
    }
  }, [])

  const closePreview = () => {
    request.current?.abort()
    request.current = null
    if (previewUrl.current) URL.revokeObjectURL(previewUrl.current)
    previewUrl.current = null
    setPreview(null)
  }

  const read = async (document) => {
    closePreview()
    setError('')
    const controller = new AbortController()
    request.current = controller
    setPreview({ title: documentLabels[document.purpose], loading: true })
    try {
      const blob = await onRead(document.id, controller.signal)
      if (controller.signal.aborted || !mounted.current) return
      if (!['image/jpeg', 'image/png'].includes(blob.type)) throw new Error('INVALID_DOCUMENT_TYPE')
      const url = URL.createObjectURL(blob)
      previewUrl.current = url
      setPreview({ title: documentLabels[document.purpose], url })
    } catch (readError) {
      if (controller.signal.aborted || !mounted.current) return
      setPreview(null)
      setError(onboardingError(readError))
    }
  }

  const upload = async (purpose, file) => {
    if (uploadGuard.current || disabled) return Upload.LIST_IGNORE
    if (!['image/jpeg', 'image/png'].includes(file.type) || file.size > 5 * 1024 * 1024) {
      setError('Chọn ảnh JPG/PNG không quá 5 MB.')
      return Upload.LIST_IGNORE
    }
    uploadGuard.current = true
    setUploading(purpose)
    setError('')
    closePreview()
    try {
      await onUpload(purpose, file)
    } catch (uploadError) {
      if (mounted.current) setError(onboardingError(uploadError))
    } finally {
      uploadGuard.current = false
      if (mounted.current) setUploading(null)
    }
    return Upload.LIST_IGNORE
  }

  const cccdProvided = documents.some((document) => document.purpose === 'FRONT' || document.purpose === 'BACK')
  return <div className="onboarding-documents">
    <Alert type="info" showIcon title="Ảnh CCCD và ảnh đại diện không bắt buộc" description="Ảnh CCCD chỉ dành cho chủ hồ sơ và quản trị viên có quyền. Ảnh được giữ tối đa 30 ngày từ lúc tải lên. Duyệt, từ chối hoặc hủy hồ sơ sẽ thu hồi truy cập và yêu cầu xóa ngay. Xóa tại nhà cung cấp có thể chậm khi dịch vụ lỗi. Ảnh đại diện không theo thời hạn này. Chỉ dùng ảnh tổng hợp khi phát triển." />
    {!cccdProvided && <Typography.Paragraph>Không cung cấp ảnh CCCD. Không thể đối chiếu ảnh khi chưa có ảnh khả dụng.</Typography.Paragraph>}
    {error && <Alert type="error" showIcon title="Không thể xử lý ảnh" description={error} />}
    <div className="onboarding-document-grid">
      {Object.entries(documentLabels).map(([purpose, label]) => {
        const matching = documents.filter((document) => document.purpose === purpose)
        const available = matching.find((document) => document.status === 'AVAILABLE')
        const latest = available || matching[matching.length - 1]
        return <section className="onboarding-document" key={purpose} aria-label={label}>
          <Typography.Text strong>{label}</Typography.Text>
          <div><Tag>{latest ? documentStatusLabels[latest.status] || latest.status : 'Không cung cấp'}</Tag></div>
          {latest?.deleteAfterUtc && <Typography.Text type="secondary">Hạn lưu: {formatOnboardingDate(latest.deleteAfterUtc, true)}</Typography.Text>}
          {latest?.deletionErrorCode && <Typography.Text type="danger">Chưa xác nhận xóa tại nhà cung cấp: {latest.deletionErrorCode}</Typography.Text>}
          <Space wrap>
            {available && <Button icon={<EyeOutlined />} htmlType="button" disabled={disabled || Boolean(uploading)} onClick={() => read(available)}>Xem {label.toLowerCase()}</Button>}
            {editable && <Upload accept="image/jpeg,image/png" showUploadList={false} disabled={disabled || Boolean(uploading)} beforeUpload={(file) => upload(purpose, file)}><Button icon={<UploadOutlined />} htmlType="button" loading={uploading === purpose} disabled={disabled || Boolean(uploading)}>{latest ? 'Thay ảnh' : 'Tải ảnh'}</Button></Upload>}
          </Space>
        </section>
      })}
    </div>
    <Modal title={preview?.title || 'Xem ảnh'} open={Boolean(preview)} onCancel={closePreview} footer={<Button onClick={closePreview}>Đóng ảnh</Button>} destroyOnHidden>
      {preview?.loading ? <Typography.Paragraph role="status">Đang tải ảnh riêng tư…</Typography.Paragraph> : preview?.url && <img className="onboarding-document-preview" src={preview.url} alt={preview.title} />}
    </Modal>
  </div>
}
