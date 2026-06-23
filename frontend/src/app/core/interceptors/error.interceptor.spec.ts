import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { errorInterceptor } from './error.interceptor';
import { AuthService } from '../services/auth.service';
import { ToastService } from '../services/toast.service';

describe('errorInterceptor', () => {
  let httpClient: HttpClient;
  let http: HttpTestingController;
  let router: Router;
  let mockAuth: { logout: jasmine.Spy; token: ReturnType<typeof signal> };
  let mockToast: { show: jasmine.Spy };

  beforeEach(() => {
    mockAuth = { logout: jasmine.createSpy('logout'), token: signal(null) };
    mockToast = { show: jasmine.createSpy('show') };

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: mockAuth },
        { provide: ToastService, useValue: mockToast },
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    httpClient = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('should call logout and navigate to /login on a 401 response', () => {
    spyOn(router, 'navigate');
    httpClient.get('/api/tasks').subscribe({ error: () => {} });

    http.expectOne('/api/tasks').flush(
      { message: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(mockAuth.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('should show an error toast using err.error.error when present', () => {
    httpClient.get('/api/tasks').subscribe({ error: () => {} });

    http.expectOne('/api/tasks').flush(
      { error: 'Task not found' },
      { status: 404, statusText: 'Not Found' },
    );

    expect(mockToast.show).toHaveBeenCalledWith('Task not found', 'error');
  });

  it('should fall back to err.error.message when error property is absent', () => {
    httpClient.get('/api/tasks').subscribe({ error: () => {} });

    http.expectOne('/api/tasks').flush(
      { message: 'Something went wrong' },
      { status: 500, statusText: 'Internal Server Error' },
    );

    expect(mockToast.show).toHaveBeenCalledWith('Something went wrong', 'error');
  });

  it('should fall back to a generic message when the error body has no usable field', () => {
    httpClient.get('/api/tasks').subscribe({ error: () => {} });

    http.expectOne('/api/tasks').flush(null, { status: 503, statusText: 'Service Unavailable' });

    expect(mockToast.show).toHaveBeenCalledWith('Unexpected error (503)', 'error');
  });

  it('should not call logout for non-401 errors', () => {
    httpClient.get('/api/tasks').subscribe({ error: () => {} });

    http.expectOne('/api/tasks').flush(
      { error: 'Bad input' },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(mockAuth.logout).not.toHaveBeenCalled();
  });
});
