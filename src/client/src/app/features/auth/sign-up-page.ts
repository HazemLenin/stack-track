import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';
import { passwordRules } from './password-rules';

const displayNameLength = (c: AbstractControl): ValidationErrors | null => {
  const length = String(c.value ?? '').trim().length;
  return length >= 2 && length <= 50 ? null : { displayNameLength: true };
};

const strongPassword = (c: AbstractControl): ValidationErrors | null =>
  passwordRules(String(c.value ?? '')).every((r) => r.met) ? null : { weakPassword: true };

const matchesPassword = (c: AbstractControl): ValidationErrors | null =>
  c.value === c.parent?.get('password')?.value ? null : { mismatch: true };

@Component({
  selector: 'app-sign-up-page',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, RouterLink],
  templateUrl: './sign-up-page.html',
  styleUrls: ['./auth-card.scss', './sign-up-page.scss'],
})
export class SignUpPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, displayNameLength]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, strongPassword]],
    confirmPassword: ['', [Validators.required, matchesPassword]],
  });

  protected readonly pending = signal(false);

  private readonly password = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });
  protected readonly rules = computed(() => passwordRules(this.password()));
  protected readonly typing = computed(() => this.password().length > 0);

  constructor() {
    // The confirm field depends on the password field, so recheck it when the password changes.
    this.form.controls.password.valueChanges.subscribe(() =>
      this.form.controls.confirmPassword.updateValueAndValidity(),
    );
  }

  protected submit(): void {
    if (this.pending()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.pending.set(true);
    const value = this.form.getRawValue();
    this.auth.register({ ...value, displayName: value.displayName.trim() }).subscribe({
      next: () => this.router.navigateByUrl('/boards'),
      error: (e: HttpErrorResponse) => {
        this.pending.set(false);
        if (e.status === 409) {
          this.form.controls.email.setErrors({ taken: true });
          this.form.controls.email.markAsTouched();
        }
      },
    });
  }
}
