import apiClient from "../../../services/apiClient";

const BASE = "/api/v1/operational-tasks";
const PAGE_SIZE = 100;

async function getAllTasks(params, signal) {
  const items = [];
  const seenIds = new Set();
  let response;
  let pageIndex = 1;
  let pageCount;

  do {
    const result = await apiClient.get(BASE, {
      params: { ...params, pageIndex, pageSize: PAGE_SIZE },
      signal,
    });
    response = result.data;
    for (const item of response.items || []) {
      if (seenIds.has(item.id)) continue;
      seenIds.add(item.id);
      items.push(item);
    }
    pageCount = Math.max(1, Math.ceil((response.totalCount || 0) / PAGE_SIZE));
    pageIndex += 1;
  } while (pageIndex <= pageCount);

  return {
    ...response,
    items,
    totalCount: items.length,
    pageIndex: 1,
    pageSize: PAGE_SIZE,
  };
}

const operationalTaskService = {
  getTasks: (params = {}, signal) => getAllTasks(params, signal),
  getChecklistResult: (taskId, signal) =>
    apiClient
      .get(`${BASE}/${taskId}/checklist-result`, { signal })
      .then(({ data }) => data),
  getOverdueTasks: async (toDate, filters, signal) => {
    const statusQueries = ["Scheduled", "Unassigned", "InProgress"].map(
      (status) => getAllTasks({ toDate, ...filters, status }, signal),
    );
    const pages = await Promise.all(statusQueries);
    const items = [
      ...new Map(
        pages.flatMap((page) => page.items).map((item) => [item.id, item]),
      ).values(),
    ].sort(
      (left, right) =>
        left.dutyDate.localeCompare(right.dutyDate) ||
        (left.scheduledAtUtc || "").localeCompare(right.scheduledAtUtc || ""),
    );
    return { items, totalCount: items.length };
  },
  getAssignees: (taskId) =>
    apiClient.get(`${BASE}/${taskId}/assignees`).then(({ data }) => data),
  assign: (taskId, payload) =>
    apiClient
      .put(`${BASE}/${taskId}/assignment`, payload)
      .then(({ data }) => data),
  cancel: (taskId, payload) =>
    apiClient
      .post(`${BASE}/${taskId}/cancel`, payload)
      .then(({ data }) => data),
  ensureChecklists: (date) =>
    apiClient
      .post(`${BASE}/checklists/ensure`, null, { params: { date } })
      .then(({ data }) => data),
  ensureWeek: (payload) =>
    apiClient
      .post(`${BASE}/weeks/ensure`, payload)
      .then(({ data }) => data),
  getWeekConfiguration: (weekStart, signal) =>
    apiClient
      .get(`${BASE}/weeks/configuration`, { params: { weekStart }, signal })
      .then(({ data }) => data),
};

export default operationalTaskService;
