import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';
import { safeReturnUrl } from './return-url';

@Component({
  selector: 'app-sign-in-page',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, RouterLink],
  templateUrl: './sign-in-page.html',
  styleUrls: ['./auth-card.scss'],
})
export class SignInPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', Validators.required],
    password: ['', Validators.required],
  });

  protected readonly pending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.pending()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.pending.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.signIn(email.trim(), password).subscribe({
      next: () => this.router.navigateByUrl(safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'))),
      error: (e: HttpErrorResponse) => {
        this.pending.set(false);
        this.error.set(this.messageFor(e));
      },
    });
  }

  private messageFor(e: HttpErrorResponse): string | null {
    if (e.status === 423 && typeof e.error?.lockedUntil === 'string') {
      const time = new Date(e.error.lockedUntil).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      return `This account is temporarily locked after too many failed attempts. Try again after ${time}.`;
    }
    if (e.status === 401 || e.status === 403) {
      return typeof e.error?.detail === 'string' ? e.error.detail : 'Email or password is incorrect';
    }
    // Other failures already show a toast through the error interceptor.
    return null;
  }
}
