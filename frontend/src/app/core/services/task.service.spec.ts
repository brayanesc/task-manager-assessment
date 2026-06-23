import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TaskService } from './task.service';
import { PagedResult, TaskItem, TaskRequest } from '../models/task.model';
import { environment } from '../../../environments/environment';

const MOCK_TASK: TaskItem = {
  id: 'task-1',
  title: 'Test task',
  description: 'A description',
  status: 'Todo',
  priority: 'Medium',
  dueDate: '2099-01-01',
  updatedAt: '2026-01-01T00:00:00Z',
};

const MOCK_PAGE: PagedResult<TaskItem> = {
  items: [MOCK_TASK],
  page: 1,
  pageSize: 5,
  totalCount: 1,
  totalPages: 1,
};

const TASK_REQUEST: TaskRequest = {
  title: 'New task',
  description: 'Details',
  status: 'Todo',
  priority: 'Low',
  dueDate: '2099-01-01',
};

describe('TaskService', () => {
  let service: TaskService;
  let http: HttpTestingController;
  const base = `${environment.apiUrl}/tasks`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TaskService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('getTasks', () => {
    it('should send GET with default page=1 and pageSize=5', () => {
      service.getTasks().subscribe();

      const req = http.expectOne(r => r.url === base);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('page')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('5');
      req.flush(MOCK_PAGE);
    });

    it('should include status and search params when provided', () => {
      service.getTasks(2, 10, 'InProgress', 'fix bug').subscribe();

      const req = http.expectOne(r => r.url === base);
      expect(req.request.params.get('page')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('10');
      expect(req.request.params.get('status')).toBe('InProgress');
      expect(req.request.params.get('search')).toBe('fix bug');
      req.flush(MOCK_PAGE);
    });

    it('should omit status and search params when not provided', () => {
      service.getTasks().subscribe();

      const req = http.expectOne(r => r.url === base);
      expect(req.request.params.has('status')).toBeFalse();
      expect(req.request.params.has('search')).toBeFalse();
      req.flush(MOCK_PAGE);
    });

    it('should trim whitespace from the search param', () => {
      service.getTasks(1, 5, undefined, '  spaces  ').subscribe();

      const req = http.expectOne(r => r.url === base);
      expect(req.request.params.get('search')).toBe('spaces');
      req.flush(MOCK_PAGE);
    });

    it('should include sortBy and sortDir when provided', () => {
      service.getTasks(1, 5, undefined, undefined, 'dueDate', 'desc').subscribe();

      const req = http.expectOne(r => r.url === base);
      expect(req.request.params.get('sortBy')).toBe('dueDate');
      expect(req.request.params.get('sortDir')).toBe('desc');
      req.flush(MOCK_PAGE);
    });
  });

  describe('createTask', () => {
    it('should send POST to /tasks with the request body', () => {
      let result: TaskItem | undefined;
      service.createTask(TASK_REQUEST).subscribe(t => (result = t));

      const req = http.expectOne(base);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(TASK_REQUEST);
      req.flush(MOCK_TASK);

      expect(result).toEqual(MOCK_TASK);
    });
  });

  describe('updateTask', () => {
    it('should send PUT to /tasks/:id with the request body', () => {
      const updated: TaskRequest = { ...TASK_REQUEST, title: 'Updated title', status: 'Done' };
      service.updateTask('task-1', updated).subscribe();

      const req = http.expectOne(`${base}/task-1`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(updated);
      req.flush({ ...MOCK_TASK, ...updated });
    });
  });

  describe('deleteTask', () => {
    it('should send DELETE to /tasks/:id', () => {
      service.deleteTask('task-42').subscribe();

      const req = http.expectOne(`${base}/task-42`);
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });
});
