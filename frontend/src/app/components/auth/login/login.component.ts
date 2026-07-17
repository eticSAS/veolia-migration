import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { CommonPrimeNgModules } from '../../../shared/primeng-imports';
import { AuthService, Sistema } from '../../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, ...CommonPrimeNgModules],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  email = '';
  password = '';
  idSistema: number | null = null;
  sistemas: Sistema[] = [];
  error = '';
  loading = false;

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  private isValidEmail(email: string): boolean {
    // eslint-disable-next-line no-useless-escape
    const re = /^(([^<>()[\]\\.,;:\s@\"]+(\.[^<>()[\]\\.,;:\s@\"]+)*)|(\".+\"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$/;
    return re.test(email);
  }

  onEmailChange(): void {
    if (this.email.length < 4) {
      this.sistemas = [];
      this.idSistema = null;
      return;
    }

    this.authService.getSistemasByCorreo(this.email).subscribe({
      next: (sistemas: Sistema[]) => {
        this.sistemas = sistemas;
        if (sistemas.length === 1) {
          this.idSistema = sistemas[0].SIST_ID;
        } else if (sistemas.length === 0) {
          this.idSistema = null;
        }
      },
      error: () => {
        this.sistemas = [];
        this.idSistema = null;
      }
    });
  }

  login(): void {
    this.error = '';

    if (!this.email || !this.password || !this.idSistema) {
      this.error = 'Complete todos los campos';
      return;
    }

    if (!this.isValidEmail(this.email)) {
      this.error = 'El email no es válido';
      return;
    }

    this.loading = true;

    this.authService.login({
      correo: this.email,
      pass: this.password,
      idSistema: this.idSistema
    }).subscribe({
      next: (response: any) => {
        this.loading = false;

        if (response.auth_token) {
          localStorage.setItem('jwtOken', response.auth_token);
          localStorage.setItem('usuario', JSON.stringify(response.usuario));
          localStorage.setItem('sistema', JSON.stringify(response.sistema));
          this.router.navigate(['/']);
        } else {
          this.error = response.message || 'Error en login';
        }
      },
      error: (err: any) => {
        this.loading = false;

        if (err.status === 401) {
          this.error = 'Usuario o Pass Incorrecto';
          this.password = '';
        } else if (err.status === 404) {
          this.error = 'Usuario no existe o inactivo';
          this.email = '';
          this.password = '';
          this.sistemas = [];
          this.idSistema = null;
        } else {
          this.error = err.error?.message || 'Error de conexión';
        }
      }
    });
  }
}
