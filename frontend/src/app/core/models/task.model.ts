export type TaskStatus = 'Todo' | 'InProgress' | 'Done';

export interface TaskItem {
  id: string;
  title: string;
  description: string;
  status: TaskStatus;
  dueDate: string;    // yyyy-MM-dd
  updatedAt: string;  // ISO 8601 UTC
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface TaskRequest {
  title: string;
  description: string;
  status: TaskStatus;
  dueDate: string;
}
