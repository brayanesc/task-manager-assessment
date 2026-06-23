import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="flex min-h-[80vh] items-center justify-center">
      <div class="card w-full max-w-sm bg-base-100 shadow-xl">
        <div class="card-body">
          <h2 class="card-title text-2xl justify-center mb-2">Sign in</h2>

          <form [formGroup]="form" (ngSubmit)="submit()">
            <!-- Email -->
            <div class="form-control mb-2">
              <label class="label"><span class="label-text">Email</span></label>
              <input
                type="email"
                formControlName="email"
                class="input input-bordered"
                [class.input-error]="dirty('email')"
                placeholder="you@example.com"
              />
              @if (dirty('email')) {
                <label class="label">
                  <span class="label-text-alt text-error">
                    @if (form.get('email')?.errors?.['required']) { Email is required. }
                    @else if (form.get('email')?.errors?.['email']) { Enter a valid email. }
                  </span>
                </label>
              }
            </div>

            <!-- Password -->
            <div class="form-control mb-4">
              <label class="label"><span class="label-text">Password</span></label>
              <input
                type="password"
                formControlName="password"
                class="input input-bordered"
                [class.input-error]="dirty('password')"
                placeholder="••••••••"
              />
              @if (dirty('password')) {
                <label class="label">
                  <span class="label-text-alt text-error">
                    @if (form.get('password')?.errors?.['required']) { Password is required. }
                  </span>
                </label>
              }
            </div>

            <div class="form-control">
              <button class="btn btn-primary w-full" [disabled]="loading()">
                @if (loading()) { <span class="loading loading-spinner loading-sm"></span> }
                Sign in
              </button>
            </div>
          </form>

          <p class="text-center text-sm mt-4">
            No account? <a routerLink="/register" class="link link-primary">Register</a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  dirty(field: string): boolean {
    const c = this.form.get(field);
    return !!(c?.invalid && (c.dirty || c.touched));
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => this.router.navigate(['/tasks']),
      error: () => this.loading.set(false),
    });
  }
}
