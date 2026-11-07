import { useState, useMemo } from 'react'
import {
  Modal,
  Descriptions,
  Tag,
  Table,
  Typography,
  Button,
  Radio,
  Input,
  Space,
  Badge,
  Segmented,
  Empty,
  notification,
  Alert,
  Image,
  message,
} from 'antd'
import dayjs from 'dayjs'
import {
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  ClockCircleOutlined,
  ToolOutlined,
  SearchOutlined,
  CameraOutlined,
  UploadOutlined,
  DeleteOutlined,
  LoadingOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import { useQueryClient } from '@tanstack/react-query'
import CameraCaptureModal from './CameraCaptureModal'
import { useResolveInspectionItem } from '../hooks/useInspections'
import { useAuthStore } from '../../../features/auth/store/authStore'
import inspectionService from '../services/inspectionService'
import poolAccessService from '../../tickets/services/poolAccessService'


const { TextArea } = Input

export default function DailyInspectionDetailsModal({
  open,
  inspection,
  onClose,
  onInspectionUpdated,
}) {
  const isAdmin = useAuthStore((state) => state.session?.role?.toUpperCase() === 'ADMIN')
  const resolveMutation = useResolveInspectionItem()

  // State lọc và tìm kiếm danh sách thiết bị
  const [userSelectedFilter, setUserSelectedFilter] = useState(null)
  const [searchText, setSearchText] = useState('')
  const [prevInspectionId, setPrevInspectionId] = useState(inspection?.id)

  // Tự động reset lựa chọn khi chuyển sang xem biên bản khác
  if (inspection?.id !== prevInspectionId) {
    setPrevInspectionId(inspection?.id)
    setUserSelectedFilter(null)
    setSearchText('')
  }

  // State cho dialog xác nhận xử lý từng thiết bị
  const [resolveTarget, setResolveTarget] = useState(null)
  const [resolveAction, setResolveAction] = useState('Repaired')
  const [resolveNote, setResolveNote] = useState('')
  const [resolveImages, setResolveImages] = useState([])
  const [isUploadingResolve, setIsUploadingResolve] = useState(false)
  const [suspendingPool, setSuspendingPool] = useState(false)
  const [cameraModalOpen, setCameraModalOpen] = useState(false)
  const queryClient = useQueryClient()

  const handleSuspendPool = () => {
    Modal.confirm({
      title: 'Xác nhận tạm dừng hoạt động bể bơi?',
      icon: <WarningOutlined style={{ color: '#dc2626' }} />,
      content: (
        <div>
          <p>
            Bạn có chắc chắn muốn chuyển trạng thái bể bơi sang <strong>Bảo trì khẩn cấp</strong> do có thiết bị cốt lõi bị hư hỏng?
          </p>
          <p style={{ color: '#dc2626', fontSize: 13 }}>
            Hệ thống sẽ tạm khóa đặt vé online và hiển thị banner thông báo bảo trì tại trang chủ cho khách hàng.
          </p>
        </div>
      ),
      okText: 'Xác nhận tạm dừng bể',
      cancelText: 'Hủy bỏ',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          setSuspendingPool(true)
          const reason = `Tạm dừng khẩn cấp do ${criticalIssueItems.map((c) => c.equipmentName).join(', ')} gặp sự cố (Biên bản ${dayjs(inspection?.inspectionDate).format('DD/MM/YYYY')}).`
          await poolAccessService.toggleMaintenance('Maintenance', reason)
          message.success('Đã chuyển trạng thái bể bơi sang Bảo trì khẩn cấp.')
          queryClient.invalidateQueries({ queryKey: ['poolLiveStatus'] })
        } catch (err) {
          message.error(err?.response?.data?.message || 'Lỗi khi tạm dừng hoạt động bể bơi.')
        } finally {
          setSuspendingPool(false)
        }
      },
    })
  }

  const allItems = useMemo(() => inspection?.items || [], [inspection])

  const issueItems = useMemo(
    () => allItems.filter((item) => !item.isPassed),
    [allItems]
  )
  const passedItems = useMemo(
    () => allItems.filter((item) => item.isPassed),
    [allItems]
  )

  // eslint-disable-next-line react-hooks/preserve-manual-memoization
  const criticalIssueItems = useMemo(
    () =>
      issueItems.filter((i) => {
        const name = (i.equipmentName || '').toLowerCase()
        return (
          i.priorityLevel === 'Critical' ||
          name.includes('bơm') ||
          name.includes('phao') ||
          name.includes('sào')
        )
      }),
    [issueItems]
  )

  // Mặc định: nếu có thiết bị sự cố thì ưu tiên chỉ hiển thị thiết bị hỏng trước ('issues'), nếu không thì 'all'
  const filterStatus = userSelectedFilter ?? (issueItems.length > 0 ? 'issues' : 'all')

  const isPassed = inspection?.overallStatus === 'Passed'
  const isResolved = inspection?.overallStatus === 'Resolved'
  const isInResolution = inspection?.overallStatus === 'InResolution'

  const openResolveDialog = (item) => {
    setResolveTarget(item)
    setResolveAction(item.resolutionStatus === 'Replaced' ? 'Replaced' : 'Repaired')
    setResolveNote(item.resolutionNote || '')
    setResolveImages(item.resolutionImageUrls || [])
  }

  const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/jpg', 'image/png']
  const ALLOWED_IMAGE_EXTS = ['.jpg', '.jpeg', '.png']

  const validateImageFiles = (files) => {
    const invalid = Array.from(files).filter((f) => {
      const ext = '.' + f.name.split('.').pop().toLowerCase()
      return !ALLOWED_IMAGE_TYPES.includes(f.type) || !ALLOWED_IMAGE_EXTS.includes(ext)
    })
    return invalid
  }

  const handleResolveImageSelected = async (files) => {
    if (!files || files.length === 0) return

    const invalidFiles = validateImageFiles(files)
    if (invalidFiles.length > 0) {
      notification.error({
        message: 'Định dạng file không được hỗ trợ',
        description: `File "${invalidFiles.map((f) => f.name).join('", "')}" không hợp lệ. Chỉ chấp nhận ảnh PNG, JPG, JPEG.`,
      })
      return
    }

    if (resolveImages.length >= 5) {
      notification.warning({ message: 'Tối đa 5 ảnh minh chứng.' })
      return
    }
    const slots = 5 - resolveImages.length
    const toUpload = Array.from(files).slice(0, slots)
    setIsUploadingResolve(true)
    try {
      const promises = toUpload.map((f) => inspectionService.uploadImage(f))
      const res = await Promise.all(promises)
      const urls = res.map((r) => r.url).filter(Boolean)
      setResolveImages((prev) => [...prev, ...urls])
      message.success(`Đã thêm ${urls.length} ảnh minh chứng.`)
    } catch (err) {
      notification.error({ message: 'Lỗi tải ảnh minh chứng', description: err.message })
    } finally {
      setIsUploadingResolve(false)
    }
  }

  const handleRemoveResolveImage = (indexToRemove) => {
    setResolveImages((prev) => prev.filter((_, idx) => idx !== indexToRemove))
  }

  const handleConfirmResolve = async () => {
    if (!resolveTarget) return

    if (resolveImages.length === 0) {
      notification.warning({
        message: 'Thiếu ảnh minh chứng',
        description: 'Vui lòng chụp ảnh hoặc tải lên ít nhất 1 ảnh minh chứng sau khi sửa chữa/thay thế mới trước khi xác nhận.',
      })
      return
    }

    try {
      const updatedInspection = await resolveMutation.mutateAsync({
        inspectionId: inspection.id,
        equipmentId: resolveTarget.equipmentId,
        action: resolveAction,
        note: resolveNote.trim() || null,
        resolutionImageUrls: resolveImages,
      })

      notification.success({
        message: 'Cập nhật thành công',
        description: `Đã xác nhận ${resolveAction === 'Replaced' ? 'thay thế' : 'sửa chữa'} thiết bị "${resolveTarget.equipmentName}". Thiết bị đã được hoàn trả về trạng thái hoạt động (Active).`,
        placement: 'topRight',
      })

      setResolveTarget(null)
      if (onInspectionUpdated) {
        onInspectionUpdated(updatedInspection)
      }
    } catch (error) {
      const serverMsg =
        error?.response?.data?.message ||
        (error?.response?.data?.errors
          ? Object.values(error.response.data.errors).flat().join(' ')
          : error?.response?.data?.title ||
            (error?.response?.status === 404
              ? 'Máy chủ trả về 404 (Chưa tìm thấy API). Hãy dừng và khởi động lại (restart) ứng dụng Backend trong Visual Studio để nhận endpoint mới.'
              : 'Có lỗi xảy ra khi cập nhật trạng thái thiết bị.'))

      notification.error({
        message: 'Cập nhật thất bại',
        description: serverMsg,
        placement: 'topRight',
      })
    }
  }

  // Lọc và sắp xếp thiết bị: sự cố luôn được ưu tiên lên đầu
  const displayedItems = useMemo(() => {
    let list = allItems

    if (filterStatus === 'issues') {
      list = issueItems
    } else if (filterStatus === 'passed') {
      list = passedItems
    }

    if (searchText.trim()) {
      const q = searchText.trim().toLowerCase()
      list = list.filter(
        (item) =>
          (item.equipmentName && item.equipmentName.toLowerCase().includes(q)) ||
          (item.category && item.category.toLowerCase().includes(q)) ||
          (item.location && item.location.toLowerCase().includes(q)) ||
          (item.note && item.note.toLowerCase().includes(q))
      )
    }

    // Sắp xếp ưu tiên:
    // 1. !isPassed lên trước
    // 2. Chờ xử lý (Pending) lên trước Đã xử lý (Repaired/Replaced)
    // 3. Tên thiết bị A-Z
    return [...list].sort((a, b) => {
      if (!a.isPassed && b.isPassed) return -1
      if (a.isPassed && !b.isPassed) return 1

      const aHandled = a.resolutionStatus === 'Repaired' || a.resolutionStatus === 'Replaced'
      const bHandled = b.resolutionStatus === 'Repaired' || b.resolutionStatus === 'Replaced'
      if (!aHandled && bHandled) return -1
      if (aHandled && !bHandled) return 1

      return (a.equipmentName || '').localeCompare(b.equipmentName || '')
    })
  }, [allItems, filterStatus, issueItems, passedItems, searchText])

  const filterOptions = useMemo(
    () => [
      {
        value: 'issues',
        label: (
          <Space size={6} style={{ padding: '0 4px' }}>
            <ExclamationCircleOutlined style={{ color: '#dc2626' }} />
            <span>Sự cố</span>
            <Badge
              count={issueItems.length}
              showZero
              style={{
                backgroundColor: issueItems.length > 0 ? '#ef4444' : '#9ca3af',
                fontSize: 11,
                lineHeight: '16px',
                height: 16,
                minWidth: 16,
              }}
            />
          </Space>
        ),
      },
      {
        value: 'passed',
        label: (
          <Space size={6} style={{ padding: '0 4px' }}>
            <CheckCircleOutlined style={{ color: '#16a34a' }} />
            <span>Đạt chuẩn</span>
            <Badge
              count={passedItems.length}
              showZero
              style={{
                backgroundColor: '#16a34a',
                fontSize: 11,
                lineHeight: '16px',
                height: 16,
                minWidth: 16,
              }}
            />
          </Space>
        ),
      },
      {
        value: 'all',
        label: (
          <Space size={6} style={{ padding: '0 4px' }}>
            <span>Tất cả</span>
            <Badge
              count={allItems.length}
              style={{
                backgroundColor: '#64748b',
                fontSize: 11,
                lineHeight: '16px',
                height: 16,
                minWidth: 16,
              }}
            />
          </Space>
        ),
      },
    ],
    [issueItems.length, passedItems.length, allItems.length]
  )

  const columns = [
    {
      title: 'Thiết bị & Khu vực',
      key: 'equipmentName',
      width: '24%',
      render: (_, item) => {
        const isCritical =
          item.priorityLevel === 'Critical' ||
          (item.equipmentName && (item.equipmentName.toLowerCase().includes('bơm') || item.equipmentName.toLowerCase().includes('phao') || item.equipmentName.toLowerCase().includes('sào')))

        return (
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
              <strong>{item.equipmentName}</strong>
              {isCritical && <Tag color="error" style={{ fontSize: 10, padding: '0 4px' }}>Cốt lõi an toàn</Tag>}
            </div>
            <div style={{ fontSize: 12, color: '#6b7280', marginTop: 2 }}>
              <Tag color="blue" style={{ fontSize: 11 }}>{item.category}</Tag>
              {item.location && <span>📍 {item.location}</span>}
            </div>
          </div>
        )
      },
    },
    {
      title: 'Đánh giá',
      key: 'isPassed',
      width: 100,
      render: (_, item) =>
        item.isPassed ? (
          <Tag icon={<CheckCircleOutlined />} color="success">
            Đạt chuẩn
          </Tag>
        ) : (
          <Tag icon={<ExclamationCircleOutlined />} color="error">
            Có vấn đề
          </Tag>
        ),
    },
    {
      title: 'Lúc hỏng (Nhân viên báo cáo)',
      key: 'defectDetails',
      width: '32%',
      render: (_, item) => {
        if (item.isPassed) return <span style={{ color: '#9ca3af' }}>—</span>
        const defectImages = item.defectImageUrls || []

        return (
          <div>
            <div style={{ color: '#dc2626', fontSize: 13, marginBottom: 4 }}>
              {item.note || 'Không có mô tả chi tiết'}
            </div>
            {defectImages.length > 0 && (
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 4 }}>
                <Image.PreviewGroup>
                  {defectImages.map((imgUrl, i) => (
                    <Image
                      key={i}
                      src={imgUrl}
                      alt="Ảnh hư hỏng"
                      width={48}
                      height={48}
                      style={{ objectFit: 'cover', borderRadius: 4, border: '1px solid #fee2e2' }}
                    />
                  ))}
                </Image.PreviewGroup>
              </div>
            )}
          </div>
        )
      },
    },
    {
      title: 'Lúc khắc phục (Admin xử lý)',
      key: 'resolution',
      width: '34%',
      render: (_, item) => {
        if (item.isPassed) {
          return <span style={{ color: '#9ca3af' }}>—</span>
        }

        const isRepaired = item.resolutionStatus === 'Repaired'
        const isReplaced = item.resolutionStatus === 'Replaced'
        const isHandled = isRepaired || isReplaced
        const resImages = item.resolutionImageUrls || []

        return (
          <div>
            {isRepaired && (
              <Tag color="success" icon={<CheckCircleOutlined />}>
                Đã sửa chữa
              </Tag>
            )}
            {isReplaced && (
              <Tag color="cyan" icon={<CheckCircleOutlined />}>
                Đã thay thế mới
              </Tag>
            )}
            {!isHandled && (
              <Tag color="warning" icon={<ClockCircleOutlined />}>
                Chờ xử lý
              </Tag>
            )}

            {isHandled && (
              <div style={{ fontSize: 11, color: '#6b7280', marginTop: 4 }}>
                {item.resolvedBy && <span>Bởi: <strong>{item.resolvedBy}</strong></span>}
                {item.resolvedAt && <span> ({dayjs(item.resolvedAt).format('HH:mm - DD/MM')})</span>}
              </div>
            )}

            {item.resolutionNote && (
              <div style={{ fontSize: 12, color: '#15803d', fontStyle: 'italic', marginTop: 2 }}>
                "{item.resolutionNote}"
              </div>
            )}

            {resImages.length > 0 && (
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 6 }}>
                <Image.PreviewGroup>
                  {resImages.map((imgUrl, i) => (
                    <Image
                      key={i}
                      src={imgUrl}
                      alt="Ảnh minh chứng"
                      width={48}
                      height={48}
                      style={{ objectFit: 'cover', borderRadius: 4, border: '1px solid #bbf7d0' }}
                    />
                  ))}
                </Image.PreviewGroup>
              </div>
            )}

            {isAdmin && (
              <div style={{ marginTop: 8 }}>
                <Button
                  type={isHandled ? 'link' : 'primary'}
                  size="small"
                  icon={<ToolOutlined />}
                  style={!isHandled ? { background: '#0284c7', borderColor: '#0284c7', fontSize: 12 } : { padding: 0, height: 'auto', fontSize: 12 }}
                  onClick={() => openResolveDialog(item)}
                >
                  {isHandled ? 'Cập nhật lại' : 'Xác nhận xử lý'}
                </Button>
              </div>
            )}
          </div>
        )
      },
    },
  ]

  const getOverallTag = () => {
    if (isPassed) {
      return <Tag color="success">Đạt chuẩn vận hành</Tag>
    }
    if (isResolved) {
      return <Tag color="green">Đã khắc phục hoàn tất (Tất cả sự cố đã xử lý)</Tag>
    }
    if (isInResolution) {
      return <Tag color="processing">Đang khắc phục sự cố</Tag>
    }
    return <Tag color="error">Phát hiện sự cố (Cần xử lý)</Tag>
  }

  if (!inspection) return null

  return (
    <>
      <Modal
        title="Chi tiết biên bản kiểm tra thiết bị đầu ca"
        open={open}
        onCancel={onClose}
        footer={null}
        width={980}
        destroyOnClose
      >
        {criticalIssueItems.length > 0 && !isResolved && (
          <Alert
            type="error"
            showIcon
            icon={<WarningOutlined style={{ fontSize: 20, color: '#dc2626' }} />}
            style={{ marginBottom: 16, border: '1px solid #fca5a5', background: '#fef2f2' }}
            message={<strong style={{ color: '#b91c1c' }}>CẢNH BÁO AN TOÀN PHÁP LÝ HỒ BƠI</strong>}
            description={
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10, marginTop: 4 }}>
                <div style={{ maxWidth: '75%', color: '#991b1b', fontSize: 13, lineHeight: 1.5 }}>
                  Biên bản ghi nhận <strong>{criticalIssueItems.length} thiết bị cốt lõi</strong> ({criticalIssueItems.map((c) => c.equipmentName).join(', ')}) bị hư hỏng, có nguy cơ vi phạm quy chuẩn an toàn bơi lội (Thông tư 03/2018/TT-BVHTTDL). Bạn có thể chủ động bấm tạm dừng hoạt động bể bơi để bảo trì ngay bên dưới.
                </div>
                {isAdmin && (
                  <Button
                    type="primary"
                    danger
                    loading={suspendingPool}
                    onClick={handleSuspendPool}
                  >
                    ⚠️ Tạm dừng hoạt động bể bơi
                  </Button>
                )}
              </div>
            }
          />
        )}

        <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }} style={{ marginBottom: 16 }}>
          <Descriptions.Item label="Ca trực">
            <Tag color={inspection.shiftName?.includes('sáng') ? 'orange' : 'purple'}>
              <strong>{inspection.shiftName || 'Ca trực'}</strong>
            </Tag>
          </Descriptions.Item>
          <Descriptions.Item label="Người kiểm tra">
            <strong>{inspection.inspectorName || 'Nhân viên'}</strong>
          </Descriptions.Item>
          <Descriptions.Item label="Thời gian">
            {dayjs(inspection.inspectionTime).format('HH:mm - DD/MM/YYYY')}
          </Descriptions.Item>
          <Descriptions.Item label="Kết quả chung">
            {getOverallTag()}
          </Descriptions.Item>
          <Descriptions.Item label="Thống kê" span={2}>
            <span>{inspection.totalItems} thiết bị ({inspection.issuesCount} hỏng hóc)</span>
          </Descriptions.Item>
          {inspection.notes && (
            <Descriptions.Item label="Ghi chú ca trực" span={2}>
              {inspection.notes}
            </Descriptions.Item>
          )}
        </Descriptions>

        {/* Thanh công cụ lọc thiết bị thông minh */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: 10,
            marginBottom: 12,
            background: '#f8fafc',
            padding: '10px 14px',
            borderRadius: 8,
            border: '1px solid #e2e8f0',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Typography.Title level={5} style={{ margin: 0, fontSize: 15 }}>
              Danh sách thiết bị kiểm tra
            </Typography.Title>
            <Tag color={filterStatus === 'issues' ? 'error' : filterStatus === 'passed' ? 'success' : 'default'}>
              Hiển thị {displayedItems.length} / {allItems.length}
            </Tag>
          </div>

          <Space wrap size="small">
            <Input
              placeholder="Tìm thiết bị..."
              prefix={<SearchOutlined style={{ color: '#9ca3af' }} />}
              allowClear
              size="middle"
              style={{ width: 180 }}
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
            />
            <Segmented
              value={filterStatus}
              onChange={setUserSelectedFilter}
              options={filterOptions}
            />
          </Space>
        </div>

        <Table
          dataSource={displayedItems}
          columns={columns}
          rowKey={(item, idx) => item.equipmentId || idx}
          pagination={
            displayedItems.length > 8
              ? {
                  pageSize: 8,
                  showSizeChanger: true,
                  pageSizeOptions: ['8', '16', '32'],
                  size: 'small',
                  showTotal: (total, range) => `${range[0]}-${range[1]} / ${total} thiết bị`,
                }
              : false
          }
          size="middle"
          locale={{
            emptyText: (
              <Empty
                description={
                  filterStatus === 'issues'
                    ? 'Không có thiết bị nào gặp sự cố'
                    : filterStatus === 'passed'
                    ? 'Không có thiết bị đạt chuẩn'
                    : 'Không tìm thấy thiết bị phù hợp'
                }
                style={{ padding: '24px 0' }}
              />
            ),
          }}
        />
      </Modal>

      {/* Modal xử lý sự cố thiết bị dành cho Admin */}
      <Modal
        title={`Xác nhận xử lý sự cố: ${resolveTarget?.equipmentName || 'Thiết bị'}`}
        open={Boolean(resolveTarget)}
        onCancel={() => setResolveTarget(null)}
        onOk={handleConfirmResolve}
        confirmLoading={resolveMutation.isPending}
        okText="Xác nhận hoàn thành"
        cancelText="Hủy"
        destroyOnClose
        width={480}
      >
        <div style={{ marginTop: 8 }}>
          <div style={{ marginBottom: 8, fontSize: 13, color: '#374151' }}>
            <strong>Sự cố ghi nhận:</strong>{' '}
            <span style={{ color: '#dc2626' }}>{resolveTarget?.note || 'Không có mô tả chi tiết'}</span>
          </div>

          <div style={{ marginBottom: 16 }}>
            <label style={{ display: 'block', marginBottom: 6, fontWeight: 600 }}>
              Hình thức xử lý <span style={{ color: '#dc2626' }}>*</span>:
            </label>
            <Radio.Group
              value={resolveAction}
              onChange={(e) => setResolveAction(e.target.value)}
              buttonStyle="solid"
            >
              <Radio.Button value="Repaired">Đã sửa chữa</Radio.Button>
              <Radio.Button value="Replaced">Đã thay thế mới</Radio.Button>
            </Radio.Group>
          </div>

          <div style={{ marginBottom: 16 }}>
            <label style={{ display: 'block', marginBottom: 6, fontWeight: 600 }}>
              Ghi chú phương án xử lý:
            </label>
            <TextArea
              rows={3}
              placeholder="VD: Đã thay gioăng vòi mới, bơm áp lực đã kiểm tra hoạt động ổn định..."
              value={resolveNote}
              onChange={(e) => setResolveNote(e.target.value)}
              maxLength={500}
              showCount
            />
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: 6, fontWeight: 600 }}>
              Ảnh minh chứng sau khi sửa chữa / thay thế mới <span style={{ color: '#dc2626' }}>*</span>:
            </label>

            {resolveImages.length > 0 && (
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 8 }}>
                <Image.PreviewGroup>
                  {resolveImages.map((imgUrl, i) => (
                    <div key={i} style={{ position: 'relative', width: 64, height: 64 }}>
                      <Image
                        src={imgUrl}
                        alt="Minh chứng"
                        width={64}
                        height={64}
                        style={{ objectFit: 'cover', borderRadius: 4, border: '1px solid #bbf7d0' }}
                      />
                      <Button
                        type="primary"
                        danger
                        shape="circle"
                        size="small"
                        icon={<DeleteOutlined style={{ fontSize: 10 }} />}
                        onClick={() => handleRemoveResolveImage(i)}
                        style={{
                          position: 'absolute',
                          top: -4,
                          right: -4,
                          width: 18,
                          height: 18,
                          minWidth: 18,
                          padding: 0,
                          lineHeight: '18px',
                        }}
                      />
                    </div>
                  ))}
                </Image.PreviewGroup>
              </div>
            )}

            {resolveImages.length < 5 && (
              <Space size="small">
                {/* Nút 1: Mở Camera trực tiếp */}
                <Button
                  size="small"
                  icon={isUploadingResolve ? <LoadingOutlined /> : <CameraOutlined />}
                  disabled={isUploadingResolve}
                  onClick={() => setCameraModalOpen(true)}
                >
                  Chụp ảnh sau khi sửa
                </Button>

                {/* Nút 2: Tải ảnh từ máy / Thư viện */}
                <label style={{ cursor: isUploadingResolve ? 'not-allowed' : 'pointer' }}>
                  <input
                    type="file"
                    accept=".jpg,.jpeg,.png,image/jpeg,image/png"
                    multiple
                    disabled={isUploadingResolve}
                    style={{ display: 'none' }}
                    onChange={(e) => {
                      handleResolveImageSelected(e.target.files)
                      e.target.value = ''
                    }}
                  />
                  <Button
                    size="small"
                    icon={<UploadOutlined />}
                    disabled={isUploadingResolve}
                    style={{ pointerEvents: 'none' }}
                  >
                    Tải ảnh minh chứng
                  </Button>
                </label>
              </Space>
            )}

            <div style={{ color: '#64748b', fontSize: 11, marginTop: 4 }}>
              * Bắt buộc có ít nhất 1 ảnh minh chứng rõ nét sau khi đã khắc phục sự cố.
            </div>
          </div>  
        </div>
      
      {cameraModalOpen && (
        <CameraCaptureModal
          open={cameraModalOpen}
          onClose={() => setCameraModalOpen(false)}
          onCapture={async (file) => {
            await handleResolveImageSelected([file])
          }}
          title={`Chụp ảnh khắc phục - ${resolveTarget?.equipmentName || ''}`}
          currentCount={resolveImages.length}
        />
      )}

      </Modal>
    </>
  )
}
