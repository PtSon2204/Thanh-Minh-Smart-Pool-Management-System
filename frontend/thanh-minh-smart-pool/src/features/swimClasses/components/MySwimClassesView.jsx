import { useState } from "react";
import {
  Alert,
  Button,
  ConfigProvider,
  Empty,
  Pagination,
  Segmented,
  Spin,
  Tag,
} from "antd";
import {
  CalendarOutlined,
  ClockCircleOutlined,
  ReadOutlined,
  ReloadOutlined,
  RightOutlined,
  UnorderedListOutlined,
  UserOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { useAuthStore } from "../../auth/store/authStore";
import { formatVietnamTime } from "../utils/classSchedule";
import { useMySwimClasses } from "../hooks/useMySwimClasses";
import "../styles/mySwimClasses.css";

const filters = [
  { label: "Tất cả", value: "all" },
  { label: "Đã đăng ký", value: "Confirmed" },
  { label: "Đã hủy", value: "Cancelled" },
];

function registrationState(item) {
  if (item.registrationStatus === "Confirmed")
    return { text: "Đã đăng ký", color: "success" };
  if (item.registrationStatus === "Cancelled")
    return { text: "Đã hủy", color: "default" };
  return { text: item.registrationStatus, color: "default" };
}

export default function MySwimClassesView() {
  const userId = useAuthStore((state) => state.session?.id);
  const navigate = useNavigate();
  const [status, setStatus] = useState("all");
  const [pageIndex, setPageIndex] = useState(1);
  const [now] = useState(() => Date.now());
  const params = {
    pageIndex,
    pageSize: 9,
    ...(status !== "all" ? { status } : {}),
  };
  const classes = useMySwimClasses(params, userId);
  const items = classes.data?.items || [];
  const upcoming = items
    .filter(
      (item) =>
        item.registrationStatus === "Confirmed" &&
        item.classStatus === "Active" &&
        item.nextSessionAt &&
        new Date(item.nextSessionAt).getTime() > now,
    )
    .sort(
      (left, right) =>
        new Date(left.nextSessionAt) - new Date(right.nextSessionAt),
    )[0];
  function openClass(item) {
    navigate(`/lop-hoc/${item.classId}`, { state: { fromMyClasses: true } });
  }

  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: "#0077b6",
          colorInfo: "#0077b6",
          borderRadius: 8,
        },
      }}
    >
      <main className="my-swim-classes-page">
        <header className="my-swim-classes-header">
          <div className="my-swim-classes-title">
            <span className="my-swim-classes-icon">
              <ReadOutlined />
            </span>
            <div>
              <small>GÓC HỌC VIÊN</small>
              <h1>Lớp học của tôi</h1>
              <p>Lịch học và tình trạng đăng ký của bạn ở một nơi.</p>
            </div>
          </div>
          <Button onClick={() => navigate("/lop-hoc")}>Tìm lớp mới</Button>
        </header>

        {!classes.isPending &&
          !classes.isError &&
          status === "all" &&
          upcoming && (
            <section
              className="my-swim-classes-overview"
              aria-label="Tổng quan học tập"
            >
              {upcoming && (
                <div className="my-swim-classes-next">
                  <span className="my-swim-classes-overview-label">
                    <ClockCircleOutlined /> BUỔI GẦN NHẤT TRÊN TRANG
                  </span>
                  <strong>{formatVietnamTime(upcoming.nextSessionAt)}</strong>
                  <p>
                    {upcoming.className} · Coach{" "}
                    {upcoming.coachName || "chưa cập nhật"}
                  </p>
                  <Button type="primary" onClick={() => openClass(upcoming)}>
                    Xem lịch lớp <RightOutlined />
                  </Button>
                </div>
              )}
            </section>
          )}

        <section
          className="my-swim-classes-panel"
          aria-label="Danh sách lớp của tôi"
        >
          <div className="my-swim-classes-toolbar">
            <div className="my-swim-classes-filter-scroll">
              <Segmented
                aria-label="Lọc theo trạng thái đăng ký"
                options={filters}
                value={status}
                onChange={(value) => {
                  setStatus(value);
                  setPageIndex(1);
                }}
              />
            </div>
            <Button
              type="text"
              icon={<ReloadOutlined spin={classes.isFetching} />}
              disabled={classes.isFetching}
              onClick={() => classes.refetch()}
            >
              Làm mới
            </Button>
          </div>

          {classes.isPending && (
            <div className="my-swim-classes-loading">
              <Spin size="large" /> Đang tải lớp học của bạn...
            </div>
          )}
          {classes.isError && (
            <Alert
              type="error"
              showIcon
              title="Không tải được lớp học của bạn"
              action={
                <Button onClick={() => classes.refetch()}>Thử lại</Button>
              }
            />
          )}
          {!classes.isPending && !classes.isError && items.length === 0 && (
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              description={
                status === "all"
                  ? "Bạn chưa đăng ký lớp học nào."
                  : "Không có lớp ở trạng thái này."
              }
            >
              {status === "all" && (
                <Button type="primary" onClick={() => navigate("/lop-hoc")}>
                  Xem lớp đang mở
                </Button>
              )}
            </Empty>
          )}
          {!classes.isPending && !classes.isError && items.length > 0 && (
            <>
              <div className="my-swim-classes-grid">
                {items.map((item) => {
                  const state = registrationState(item);
                  const canOpen = item.classStatus === "Active";
                  return (
                    <article
                      className="my-swim-class-card"
                      key={item.registrationId}
                    >
                      <div className="my-swim-class-card-top">
                        <Tag color={state.color}>{state.text}</Tag>
                        {item.classStatus !== "Active" && (
                          <Tag color="default">Lớp ngừng hoạt động</Tag>
                        )}
                      </div>
                      <h2>{item.className}</h2>
                      <div className="my-swim-class-next-session">
                        <span>
                          <CalendarOutlined /> Buổi học tiếp theo
                        </span>
                        <strong>
                          {item.nextSessionAt
                            ? formatVietnamTime(item.nextSessionAt)
                            : "Chưa có buổi sắp tới"}
                        </strong>
                      </div>
                      <dl>
                        <div>
                          <dt>
                            <UserOutlined /> Coach
                          </dt>
                          <dd>{item.coachName || "Chưa cập nhật"}</dd>
                        </div>
                        <div>
                          <dt>
                            <UnorderedListOutlined /> Tổng số buổi
                          </dt>
                          <dd>{item.sessionCount} buổi</dd>
                        </div>
                      </dl>
                      <p className="my-swim-class-registered-at">
                        Đăng ký lúc {formatVietnamTime(item.registeredAt)}
                      </p>
                      <div className="my-swim-class-card-actions">
                        {canOpen ? (
                          <Button
                            icon={<RightOutlined />}
                            onClick={() => openClass(item)}
                          >
                            Xem lớp học
                          </Button>
                        ) : (
                          <span>Chi tiết lớp hiện không khả dụng.</span>
                        )}
                      </div>
                    </article>
                  );
                })}
              </div>
              <Pagination
                className="my-swim-classes-pagination"
                current={pageIndex}
                pageSize={params.pageSize}
                total={classes.data?.totalCount || 0}
                showSizeChanger={false}
                onChange={setPageIndex}
                showTotal={(total) => `${total.toLocaleString("vi-VN")} lớp`}
              />
            </>
          )}
        </section>
      </main>
    </ConfigProvider>
  );
}
