import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult, TaskItem, TaskRequest } from '../models/task.model';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/tasks`;

  getTasks(
    page = 1,
    pageSize = 5,
    status?: string,
    search?: string,
    sortBy?: string,
    sortDir?: 'asc' | 'desc',
  ): Observable<PagedResult<TaskItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    if (search?.trim()) params = params.set('search', search.trim());
    if (sortBy) params = params.set('sortBy', sortBy);
    if (sortDir) params = params.set('sortDir', sortDir);
    return this.http.get<PagedResult<TaskItem>>(this.base, { params });
  }

  createTask(req: TaskRequest): Observable<TaskItem> {
    return this.http.post<TaskItem>(this.base, req);
  }

  updateTask(id: string, req: TaskRequest): Observable<TaskItem> {
    return this.http.put<TaskItem>(`${this.base}/${id}`, req);
  }

  deleteTask(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
