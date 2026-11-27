import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  Alert,
  Avatar,
  Button,
  DatePicker,
  Descriptions,
  Drawer,
  Pagination,
  Select,
  Segmented,
  Space,
  Spin,
  Table,
  Tabs,
  Tag,
  Typography,
} from "antd";
import dayjs from "dayjs";
import { useStaffSchedule } from "../hooks/useStaffs";
import StaffWorkPatternPanel from "./StaffWorkPatternPanel";
import staffService from "../services/staffService";
import { USER_ROLES } from "../../users/utils/userManagement";

function formatDate(value) {
  return value
    ? new Intl.DateTimeFormat("vi-VN", { dateStyle: "short" }).format(
        new Date(value),
      )
    : "-";
}

function formatAttendanceTime(value) {
  return value
    ? new Intl.DateTimeFormat("vi-VN", {
        dateStyle: "short",
        timeStyle: "short",
        timeZone: "Asia/Ho_Chi_Minh",
      }).format(new Date(value))
    : "-";
}

function QueryTable({ query, columns, emptyText, pagination, onPageChange }) {
  if (query.isLoading)
    return (
      <div className="admin-management-state">
        <Spin size="large" />
      </div>
    );
  if (query.isError)
    return (
      <Alert
        type="error"
        showIcon
        title="Không thể tải dữ liệu"
        description="Hãy thử lại sau."
        action={
          <Button size="small" onClick={query.refetch}>
            Thử lại
          </Button>
        }
      />
    );
  return (
    <>
      <Table
        rowKey="id"
        columns={columns}
        dataSource={query.data?.items || []}
        pagination={false}
        scroll={{ x: true }}
        locale={{ emptyText }}
      />
      {(query.data?.totalCount || 0) > 0 && (
        <div className="admin-management-pagination">
          <Pagination
            current={pagination.pageIndex}
            pageSize={pagination.pageSize}
            total={query.data.totalCount}
            showSizeChanger
            onChange={onPageChange}
          />
        </div>
      )}
    </>
  );
}

function ScheduleContent({ employeeId, attendance }) {
  const [date, setDate] = useState();
  const [status, setStatus] = useState();
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const query = useStaffSchedule(
    employeeId,
    { date, status, pageIndex, pageSize },
    true,
  );
  const columns = attendance
    ? [
        { title: "Ngày", dataIndex: "workDate", render: formatDate },
        { title: "Ca", dataIndex: "shiftName" },
        {
          title: "Vào ca",
          dataIndex: "checkInTime",
          render: formatAttendanceTime,
        },
        {
          title: "Ra ca",
          dataIndex: "checkOutTime",
          render: formatAttendanceTime,
        },
        {
          title: "Trạng thái",
          dataIndex: "status",
          render: (value) => <Tag>{value}</Tag>,
        },
      ]
    : [
        { title: "Ngày làm", dataIndex: "workDate", render: formatDate },
        { title: "Ca", dataIndex: "shiftName" },
        { title: "Bắt đầu", dataIndex: "startTime" },
        { title: "Kết thúc", dataIndex: "endTime" },
        {
          title: "Trạng thái",
          dataIndex: "status",
          render: (value) => <Tag>{value}</Tag>,
        },
      ];
  return (
    <>
      <div className="service-list-filters">
        <DatePicker
          allowClear
          value={date ? dayjs(date) : null}
          onChange={(value) => {
            setDate(value?.format("YYYY-MM-DD"));
            setPageIndex(1);
          }}
          placeholder="Lọc theo ngày"
        />
        <Select
          allowClear
          value={status}
          onChange={(value) => {
            setStatus(value);
            setPageIndex(1);
          }}
          placeholder="Lọc trạng thái"
          options={[
            { value: "Scheduled", label: "Đã lên lịch" },
            { value: "Present", label: "Có mặt" },
            { value: "Completed", label: "Hoàn thành" },
            { value: "Cancelled", label: "Đã hủy" },
          ]}
        />
      </div>
      <QueryTable
        query={query}
        columns={columns}
        emptyText={attendance ? "Chưa có chấm công." : "Chưa có lịch làm việc."}
        pagination={{ pageIndex, pageSize }}
        onPageChange={(page, size) => {
          setPageIndex(page);
          setPageSize(size);
        }}
      />
    </>
  );
}

export default function StaffDetailsDrawer({ open, onClose, staff }) {
  const [tab, setTab] = useState("profile");
  const [mode, setMode] = useState("schedule");
  const protectedQuery = useQuery({
    queryKey: ["staff-edit", staff?.userId],
    queryFn: ({ signal }) => staffService.getEdit(staff.userId, signal),
    enabled: open && tab === "profile" && Boolean(staff?.userId),
    retry: false, gcTime: 0,
  });
  if (!staff) return null;
  const details = protectedQuery.data?.details;
  const show = (value) => value || "Chưa có thông tin";
  const role = USER_ROLES.find((item) => item.value === staff.roleName?.toUpperCase())?.label || staff.roleName || "Chưa có vai trò";
  const profile = (
    <div className="staff-contract-stack">
      <Space align="start" wrap>
        <Avatar size={64} src={staff.avatarUrl}>{(staff.fullName || "N").charAt(0)}</Avatar>
        <div><Typography.Title level={4}>{staff.fullName || "Nhân viên chưa có tên"}</Typography.Title>
          <div>{staff.companyEmail || "Chưa có email công ty"}</div>
          <Space wrap><Tag>{role}</Tag><Tag color={staff.accountStatus?.toUpperCase() === "ACTIVE" ? "success" : "default"}>{staff.accountStatus?.toUpperCase() === "ACTIVE" ? "Tài khoản hoạt động" : staff.accountStatus?.toUpperCase() === "LOCKED" ? "Tài khoản bị khóa" : "Chưa rõ trạng thái tài khoản"}</Tag></Space>
        </div>
      </Space>
      {protectedQuery.isLoading && <Spin aria-label="Đang tải thông tin cá nhân" />}
      {protectedQuery.isError && <Alert type="warning" showIcon title="Không thể tải thông tin riêng tư" description="Chỉ thông tin tóm tắt được hiển thị." action={<Button size="small" onClick={() => protectedQuery.refetch()}>Thử lại</Button>} />}
      <Descriptions title="Thông tin cá nhân" bordered column={{ xs: 1, sm: 2 }} size="small">
        <Descriptions.Item label="Họ và tên">{show(staff.fullName)}</Descriptions.Item>
        <Descriptions.Item label="Điện thoại">{show(staff.phone)}</Descriptions.Item>
        <Descriptions.Item label="Email cá nhân">{show(details?.personalEmail ?? staff.email)}</Descriptions.Item>
        <Descriptions.Item label="Ngày sinh">{formatDate(details?.dateOfBirth ?? staff.dateOfBirth)}</Descriptions.Item>
        {details && <Descriptions.Item label="Giới tính">{details.gender === "MALE" ? "Nam" : details.gender === "FEMALE" ? "Nữ" : "Chưa có thông tin"}</Descriptions.Item>}
        <Descriptions.Item label="Địa chỉ liên hệ">{show(details?.address ?? staff.address)}</Descriptions.Item>
      </Descriptions>
      {details && <Descriptions title="Thông tin pháp lý" bordered column={{ xs: 1, sm: 2 }} size="small">
        <Descriptions.Item label="Số CCCD">{show(details.cccdNumber)}</Descriptions.Item>
        <Descriptions.Item label="Địa chỉ thường trú">{show(details.permanentAddress)}</Descriptions.Item>
        <Descriptions.Item label="Địa chỉ tạm trú">{show(details.temporaryAddress)}</Descriptions.Item>
        <Descriptions.Item label="Ngân hàng">{show(details.bankName)}</Descriptions.Item>
        <Descriptions.Item label="Số tài khoản">{show(details.bankAccountNumber)}</Descriptions.Item>
        <Descriptions.Item label="Loại mã số thuế">{details.taxIdentifierKind === "PERSONAL_ID" ? "CCCD" : details.taxIdentifierKind === "LEGACY_TAX_CODE" ? "Mã số thuế" : show(details.taxIdentifierKind)}</Descriptions.Item>
        <Descriptions.Item label="Mã số thuế">{show(details.taxIdentifier)}</Descriptions.Item>
        {details.emergencyContactName && <Descriptions.Item label="Người liên hệ khẩn cấp">{details.emergencyContactName}</Descriptions.Item>}
        {details.emergencyContactPhone && <Descriptions.Item label="Điện thoại khẩn cấp">{details.emergencyContactPhone}</Descriptions.Item>}
        {details.emergencyContactRelationship && <Descriptions.Item label="Quan hệ">{details.emergencyContactRelationship}</Descriptions.Item>}
      </Descriptions>}
      <Descriptions title="Thông tin tài khoản" bordered column={{ xs: 1, sm: 2 }} size="small">
        <Descriptions.Item label="Email công ty">{staff.companyEmail || "Chưa có email công ty"}</Descriptions.Item>
        <Descriptions.Item label="Vai trò">{role}</Descriptions.Item>
        <Descriptions.Item label="Ngày vào làm">{formatDate(staff.joinDate)}</Descriptions.Item>
        <Descriptions.Item label="Trạng thái việc làm">{staff.status === "Working" ? "Đang làm việc" : staff.status === "Inactive" ? "Không hoạt động" : show(staff.status)}</Descriptions.Item>
      </Descriptions>
    </div>
  );
  return (
    <Drawer
      rootClassName="staff-profiles-drawer"
      title={`Hồ sơ: ${staff.fullName || "Nhân viên"}`}
      open={open}
      onClose={onClose}
      size="large"
    >
      <Tabs
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: "profile",
            label: "Hồ sơ",
            children: profile,
          },
          {
            key: "schedule",
            label: "Lịch & Chấm công",
            title: "Lịch làm việc",
            children:
              open && tab === "schedule" ? (<div className="staff-contract-stack"><Segmented aria-label="Chọn chế độ xem" value={mode} onChange={setMode} options={[{ label: "Lịch", value: "schedule" }, { label: "Chấm công", value: "attendance" }]} /><ScheduleContent key={`${staff.userId}:${mode}`} employeeId={staff.userId} attendance={mode === "attendance"} /></div>) : null,
          },
          {
            key: "work-pattern",
            label: "Hợp đồng",
            title: "Hợp đồng và lịch tuần",
            children:
              open && tab === "work-pattern" ? (
                <StaffWorkPatternPanel
                  key={staff.userId}
                  employeeId={staff.userId}
                />
              ) : null,
          },
        ]}
      />
    </Drawer>
  );
}
