/**
 * PrimeNG Shared Imports
 * 
 * Centraliza todos los imports de PrimeNG para evitar duplicación.
 * Uso en componentes standalone:
 *   imports: [...CommonPrimeNgModules, CommonModule, FormsModule]
 */

import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { TabsModule } from 'primeng/tabs';
import { InputNumberModule } from 'primeng/inputnumber';
import { SkeletonModule } from 'primeng/skeleton';
import { PickListModule } from 'primeng/picklist';

export const CommonPrimeNgModules = [
  SelectModule,
  InputTextModule,
  TextareaModule,
  CheckboxModule,
  ButtonModule,
  CardModule,
  TableModule,
  TooltipModule,
  TabsModule,
  InputNumberModule,
  SkeletonModule,
  PickListModule
] as const;
