import { useEffect, useState } from "react";
import {
  Alert,
  Button,
  Empty,
  Image,
  Input,
  Modal,
  Pagination,
  Spin,
  Tag,
  message,
} from "antd";
import {
  ArrowLeftOutlined,
  CameraOutlined,
  EditOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuthStore } from "../../auth/store/authStore";
import {
  useRecordClassAttendance,
  useSessionAttendances,
  useUpdateClassProgressNote,
} from "../hooks/useClassAttendances";
import { swimClassService } from "../services/swimClassService";
import { attendanceStatus } from "../utils/classAttendance";
import { formatVietnamTime } from "../utils/classSchedule";
import { classServerMessage } from "../utils/classDisplay";
import CameraCaptureModal from "../../equipments/components/CameraCaptureModal";
import "../styles/classAttendance.css";

export default function CoachSessionAttendancesView() {
  const { classId, sessionId } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const coachId = useAuthStore((state) => state.session?.id);

  const [status, setStatus] = useState("all");
  const [pageIndex, setPageIndex] = useState(1);
  const [selected, setSelected] = useState(null);
  const [modalMode, setModalMode] = useState(null);
  const [progressNote, setProgressNote] = useState("");
  const [image, setImage] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [cameraOpen, setCameraOpen] = useState(false);
  const [error, setError] = useState(null);
  const [now, setNow] = useState(() => Date.now());

  const params = {
    pageIndex,
    pageSize: 10,
    ...(status !== "all" ? { status } : {}),
  };

  const attendances = useSessionAttendances(
    classId,
    sessionId,
    coachId,
    params,
  );

  const record = useRecordClassAttendance(classId, sessionId);
  const updateNote = useUpdateClassProgressNote(classId, sessionId);
  const classDetail = useQuery({
    queryKey: ["swim-class-detail", classId],
    queryFn: ({ signal }) => swimClassService.getClassById(classId, undefined, signal),
    enabled: Boolean(classId),
  });
  const items = attendances.data?.items || [];
  const session = classDetail.data?.sessions?.find((item) => item.id === sessionId);
  const endsAt = session?.endsAt || location.state?.endsAt;
  const classStatus = classDetail.data?.status || location.state?.classStatus;

  const isSessionOngoing = Boolean(
    endsAt && Date.parse(endsAt) >= now,
  );
  const canEditNote = Boolean(selected?.attendanceId && endsAt &&
    Date.parse(endsAt) <= now && session?.status !== "Cancelled");

  useEffect(() => {
    const timer = setInterval(() => {
      setNow(Date.now());
    }, 1000);

    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    return () => {
      if (imagePreview) {
        URL.revokeObjectURL(imagePreview);
      }
    };
  }, [imagePreview]);

  function handleImageChange(file) {
    setImage(file);
    setImagePreview(file ? URL.createObjectURL(file) : null);
  }

  function resetModal() {
    setSelected(null);
    setModalMode(null);
    setProgressNote("");
    handleImageChange(null);
    setCameraOpen(false);
    setError(null);
  }

  function close() {
    if (record.isPending || updateNote.isPending) return;
    resetModal();
  }

  function choose(item, mode) {
    setSelected(item);
    setModalMode(mode);
    setProgressNote(item.progressNote || "");
    handleImageChange(null);
    setError(null);
  }

  async function saveNote() {
    if (!selected || modalMode !== "note" || !canEditNote || updateNote.isPending) return;
    if (!progressNote.trim()) {
      setError("Vui lòng nhập nhận xét.");
      return;
    }
    setError(null);
    try {
      await updateNote.mutateAsync({
        registrationId: selected.registrationId,
        progressNote: progressNote.trim(),
        expectedUpdatedAt: selected.updatedAt,
      });
      message.success("Đã lưu nhận xét học viên.");
      resetModal();
    } catch (err) {
      setError(classServerMessage(err, "Không lưu được nhận xét. Vui lòng thử lại."));
      if (err?.response?.status === 409) void attendances.refetch();
    }
  }

  async function save(result) {
    if (!selected || modalMode !== "attendance" || record.isPending) return;

    if (result === "Absent" && isSessionOngoing) {
      setError("Chỉ được đánh dấu vắng sau khi buổi học kết thúc.");
      return;
    }

    if (result === "Verified" && !image && !selected.imageUrl) {
      setError("Vui lòng chụp ảnh học viên trước khi xác nhận có mặt.");
      return;
    }

    setError(null);

    try {
      await record.mutateAsync({
        registrationId: selected.registrationId,
        status: result,
        image: result === "Verified" ? image : null,
        expectedUpdatedAt: selected.updatedAt,
      });

      message.success(
        result === "Verified"
          ? "Đã xác nhận học viên có mặt."
          : "Đã đánh dấu học viên vắng mặt.",
      );

      resetModal();
    } catch (err) {
      setError(
        classServerMessage(err, "Không lưu được điểm danh. Vui lòng thử lại."),
      );

      if (err?.response?.status === 409) {
        void attendances.refetch();
      }
    }
  }

  return (
    <main className="class-attendance-page class-attendance-page--coach">
      <Button
        type="text"
        icon={<ArrowLeftOutlined />}
        onClick={() => navigate(`/coach/classes/${classId}`)}
      >
        Về chi tiết lớp
      </Button>

      <header className="class-attendance-header">
        <div>
          <small>GÓC COACH</small>
          <h1>Điểm danh buổi học</h1>

          {location.state?.startsAt && (
            <p>
              {formatVietnamTime(location.state.startsAt)} –{" "}
              {formatVietnamTime(location.state.endsAt).slice(-5)}
            </p>
          )}
        </div>
      </header>

      <section
        className="class-attendance-panel"
        aria-label="Điểm danh của buổi học"
      >
        <div className="class-attendance-toolbar">
          <select
            aria-label="Lọc trạng thái"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPageIndex(1);
            }}
          >
            <option value="all">Tất cả</option>
            <option value="NotMarked">Chưa điểm danh</option>
            <option value="Verified">Có mặt</option>
            <option value="Absent">Vắng mặt</option>
          </select>

          <Button
            icon={<ReloadOutlined />}
            onClick={() => attendances.refetch()}
          >
            Làm mới
          </Button>
        </div>

        {attendances.isPending && <Spin />}

        {attendances.isError && (
          <Alert
            type="error"
            showIcon
            title="Không tải được danh sách học viên."
          />
        )}

        {!attendances.isPending && !attendances.isError && !items.length && (
          <Empty description="Không có học viên ở trạng thái này." />
        )}

        <div className="class-attendance-list">
          {items.map((item) => (
            <article className="class-attendance-row" key={item.registrationId}>
              {item.imageUrl && (
                <Image
                  width={92}
                  height={68}
                  style={{ objectFit: "cover" }}
                  src={item.imageUrl}
                  alt={`Ảnh điểm danh của ${item.customerName}`}
                />
              )}

              <div className="class-attendance-row-main">
                <strong>{item.customerName}</strong>
                {item.progressNote && <span>Nhận xét: {item.progressNote}</span>}

                {item.submittedAt && (
                  <span>
                    Ghi nhận lúc {formatVietnamTime(item.submittedAt)}
                  </span>
                )}
              </div>

              <Tag color={attendanceStatus[item.status]?.color || "default"}>
                {attendanceStatus[item.status]?.label || item.status}
              </Tag>

              <div className="class-attendance-actions">
                <Button
                  icon={<CameraOutlined />}
                  disabled={classStatus === "Completed"}
                  onClick={() => choose(item, "attendance")}
                >
                  Điểm danh / sửa
                </Button>
                <Button
                  icon={<EditOutlined />}
                  disabled={!item.attendanceId || !endsAt ||
                    Date.parse(endsAt) > now || session?.status === "Cancelled"}
                  onClick={() => choose(item, "note")}
                >
                  {item.progressNote ? "Sửa nhận xét" : "Nhận xét"}
                </Button>
              </div>
            </article>
          ))}
        </div>

        {(attendances.data?.totalCount || 0) > 10 && (
          <Pagination
            current={pageIndex}
            pageSize={10}
            total={attendances.data.totalCount}
            showSizeChanger={false}
            onChange={setPageIndex}
          />
        )}
      </section>

      <Modal
        rootClassName="class-attendance-action-modal"
        title={`${modalMode === "note" ? "Nhận xét" : "Điểm danh"}: ${selected?.customerName || ""}`}
        open={Boolean(selected)}
        onCancel={close}
        footer={modalMode === "note" ? (
          <>
            <Button onClick={close}>Đóng</Button>
            <Button
              type="primary"
              loading={updateNote.isPending}
              disabled={!canEditNote || !progressNote.trim() || progressNote.length > 2000}
              onClick={saveNote}
            >
              Lưu nhận xét
            </Button>
          </>
        ) : (
          <>
            <Button onClick={close}>Đóng</Button>
            <Button
              danger
              loading={record.isPending}
              disabled={isSessionOngoing || classStatus === "Completed"}
              onClick={() => save("Absent")}
            >
              Đánh dấu vắng
            </Button>
            <Button
              type="primary"
              loading={record.isPending}
              disabled={classStatus === "Completed"}
              onClick={() => save("Verified")}
            >
              Xác nhận có mặt
            </Button>
          </>
        )}
      >
        {modalMode === "note" ? (
          <>
            <label htmlFor="class-progress-note">Nhận xét riêng cho học viên sau buổi học</label>
            <Input.TextArea
              id="class-progress-note"
              value={progressNote}
              onChange={(event) => {
                setProgressNote(event.target.value);
                setError(null);
              }}
              maxLength={2000}
              showCount
              rows={4}
              status={progressNote.trim() ? undefined : "error"}
              aria-invalid={!progressNote.trim()}
              aria-describedby={!progressNote.trim() ? "class-progress-note-error" : undefined}
              disabled={updateNote.isPending}
              placeholder="Ví dụ: Đã tập thở dưới nước; buổi sau luyện nổi."
            />
            {!progressNote.trim() && <p id="class-progress-note-error" className="class-attendance-rejection">Vui lòng nhập nhận xét.</p>}
          </>
        ) : (
          <>
            <p>
              Coach có thể sửa điểm danh khi lớp còn hoạt động. Chỉ đánh dấu vắng
              sau buổi học.
            </p>
            <Button
              icon={<CameraOutlined />}
              disabled={record.isPending}
              onClick={() => setCameraOpen(true)}
            >
              Chụp ảnh học viên
            </Button>
            {image && <p>Ảnh mới: {image.name}</p>}
            {image && imagePreview && (
              <Image width={160} src={imagePreview} alt="Ảnh mới chuẩn bị xác nhận" />
            )}
            {!image && selected?.imageUrl && (
              <Image width={160} src={selected.imageUrl} alt="Ảnh điểm danh hiện tại" />
            )}
          </>
        )}

        {error && <Alert type="error" showIcon title={error} />}
      </Modal>

      {cameraOpen && (
        <CameraCaptureModal
          open={cameraOpen}
          onClose={() => setCameraOpen(false)}
          mobileClassName="class-attendance-camera"
          onCapture={(file) => {
            if (
              !["image/jpeg", "image/png"].includes(file.type) ||
              file.size > 5 * 1024 * 1024
            ) {
              throw new Error("Ảnh phải là JPG/PNG tối đa 5 MB.");
            }

            handleImageChange(file);
            setCameraOpen(false);
          }}
          title="Chụp ảnh học viên"
          maxPhotos={1}
          currentCount={0}
          captureHint="Chụp rõ học viên đang có mặt tại lớp"
        />
      )}
    </main>
  );
}
