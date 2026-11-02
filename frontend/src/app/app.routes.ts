import { Routes } from '@angular/router';
import { CatalogPage } from './pages/catalog.page';
import { CourseDetailPage } from './pages/course-detail.page';
import { ForgotPasswordPage, LoginPage, RegisterPage, ResetPasswordPage, VerifyEmailPage } from './pages/auth.pages';

export const routes: Routes = [
  { path: '', component: CatalogPage, title: 'Explore courses · Open Learning' },
  { path: 'courses/:id', component: CourseDetailPage, title: 'Course · Open Learning' },
  { path: 'login', component: LoginPage, title: 'Sign in · Open Learning' },
  { path: 'register', component: RegisterPage, title: 'Create account · Open Learning' },
  { path: 'verify-email', component: VerifyEmailPage, title: 'Confirm email · Open Learning' },
  { path: 'forgot-password', component: ForgotPasswordPage, title: 'Reset password · Open Learning' },
  { path: 'reset-password', component: ResetPasswordPage, title: 'Choose a new password · Open Learning' },
  { path: '**', redirectTo: '' },
];
