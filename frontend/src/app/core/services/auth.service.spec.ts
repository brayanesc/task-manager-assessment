import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('should start unauthenticated when localStorage is empty', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token()).toBeNull();
  });

  it('should set token and mark authenticated after login', () => {
    service.login({ email: 'user@test.com', password: 'pass' }).subscribe();

    const req = http.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush({ token: 'jwt-abc', email: 'user@test.com' });

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.token()).toBe('jwt-abc');
  });

  it('should persist token to localStorage after login', () => {
    service.login({ email: 'user@test.com', password: 'pass' }).subscribe();
    http.expectOne(`${environment.apiUrl}/auth/login`).flush({ token: 'jwt-abc', email: 'user@test.com' });

    expect(localStorage.getItem('tm_token')).toBe('jwt-abc');
  });

  it('should set token and mark authenticated after register', () => {
    service.register({ email: 'new@test.com', password: 'pass' }).subscribe();

    const req = http.expectOne(`${environment.apiUrl}/auth/register`);
    expect(req.request.method).toBe('POST');
    req.flush({ token: 'jwt-xyz', email: 'new@test.com' });

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.token()).toBe('jwt-xyz');
  });

  it('should clear token and mark unauthenticated after logout', () => {
    service.login({ email: 'user@test.com', password: 'pass' }).subscribe();
    http.expectOne(`${environment.apiUrl}/auth/login`).flush({ token: 'jwt-abc', email: 'user@test.com' });

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token()).toBeNull();
    expect(localStorage.getItem('tm_token')).toBeNull();
  });

  it('should restore session from localStorage on init', () => {
    localStorage.setItem('tm_token', 'stored-jwt');
    localStorage.setItem('tm_email', 'user@test.com');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const freshService = TestBed.inject(AuthService);

    expect(freshService.isAuthenticated()).toBeTrue();
    expect(freshService.token()).toBe('stored-jwt');
  });
});
