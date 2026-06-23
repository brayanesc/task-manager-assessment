import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div style="display:flex;min-height:100vh;background:var(--bg);">
      <!-- Left panel — hidden below md -->
      <div class="hidden md:flex" style="
        width:420px;flex-shrink:0;background:var(--accent);
        flex-direction:column;align-items:center;justify-content:center;
        padding:48px 40px;gap:24px;
      ">
        <div style="display:flex;align-items:center;gap:12px;">
          <div style="width:40px;height:40px;border-radius:10px;background:rgba(255,255,255,.18);
            display:flex;align-items:center;justify-content:center;">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5"
              stroke-linecap="round" stroke-linejoin="round">
              <polyline points="20 6 9 17 4 12"/>
            </svg>
          </div>
          <span style="color:#fff;font-size:20px;font-weight:700;letter-spacing:-.3px;">Task Manager</span>
        </div>
        <p style="color:rgba(255,255,255,.75);font-size:15px;text-align:center;line-height:1.6;max-width:280px;margin:0;">
          Organize your work, track progress, and hit every deadline — all in one place.
        </p>
        <p style="color:rgba(255,255,255,.45);font-size:13px;margin:0;">&copy; 2026 Task Manager</p>
      </div>

      <!-- Right panel -->
      <div style="flex:1;display:flex;align-items:center;justify-content:center;padding:32px 24px;">
        <div style="width:100%;max-width:380px;">
          <!-- Logo (mobile only) -->
          <div class="flex md:hidden" style="align-items:center;gap:10px;margin-bottom:32px;">
            <div style="width:34px;height:34px;border-radius:8px;background:var(--accent);
              display:flex;align-items:center;justify-content:center;">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5"
                stroke-linecap="round" stroke-linejoin="round">
                <polyline points="20 6 9 17 4 12"/>
              </svg>
            </div>
            <span style="font-size:17px;font-weight:700;color:var(--text);">Task Manager</span>
          </div>

          <h1 style="font-size:26px;font-weight:800;color:var(--text);margin:0 0 6px;">Create account</h1>
          <p style="font-size:14px;color:var(--dim);margin:0 0 32px;">Start organizing your tasks today.</p>

          <form [formGroup]="form" (ngSubmit)="submit()">
            <div style="margin-bottom:16px;">
              <label style="display:block;font-size:13px;font-weight:600;color:var(--text);margin-bottom:6px;">
                Email
              </label>
              <input
                type="email"
                formControlName="email"
                placeholder="you@example.com"
                style="
                  width:100%;padding:10px 14px;border-radius:8px;font-size:14px;
                  border:1px solid var(--border-strong);background:var(--surface);color:var(--text);
                  outline:none;font-family:inherit;transition:border-color .15s;
                "
                [style.border-color]="dirty('email') ? '#e5484d' : 'var(--border-strong)'"
              />
              @if (dirty('email')) {
                <p style="font-size:12px;color:#e5484d;margin:4px 0 0;">
                  @if (form.get('email')?.errors?.['required']) { Email is required. }
                  @else if (form.get('email')?.errors?.['email']) { Enter a valid email. }
                </p>
              }
            </div>

            <div style="margin-bottom:24px;">
              <label style="display:block;font-size:13px;font-weight:600;color:var(--text);margin-bottom:6px;">
                Password
              </label>
              <input
                type="password"
                formControlName="password"
                placeholder="Min 8 characters"
                style="
                  width:100%;padding:10px 14px;border-radius:8px;font-size:14px;
                  border:1px solid var(--border-strong);background:var(--surface);color:var(--text);
                  outline:none;font-family:inherit;transition:border-color .15s;
                "
                [style.border-color]="dirty('password') ? '#e5484d' : 'var(--border-strong)'"
              />
              @if (dirty('password')) {
                <p style="font-size:12px;color:#e5484d;margin:4px 0 0;">
                  @if (form.get('password')?.errors?.['required']) { Password is required. }
                  @else if (form.get('password')?.errors?.['minlength']) { At least 8 characters. }
                </p>
              }
            </div>

            <button
              type="submit"
              [disabled]="loading()"
              style="
                width:100%;padding:11px;border-radius:8px;font-size:14px;font-weight:600;
                background:var(--accent);color:var(--accent-fg);border:none;cursor:pointer;
                font-family:inherit;transition:opacity .15s;
              "
              [style.opacity]="loading() ? '0.7' : '1'"
            >
              @if (loading()) { Creating… } @else { Create account }
            </button>
          </form>

          <p style="text-align:center;font-size:13px;color:var(--dim);margin-top:24px;">
            Already have an account?
            <a routerLink="/login" style="color:var(--accent);font-weight:600;text-decoration:none;">Sign in</a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  dirty(field: string): boolean {
    const c = this.form.get(field);
    return !!(c?.invalid && (c.dirty || c.touched));
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => this.router.navigate(['/tasks']),
      error: () => this.loading.set(false),
    });
  }
}
