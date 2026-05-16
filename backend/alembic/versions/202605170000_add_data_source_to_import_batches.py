"""add data_source to import_batches

Revision ID: f1a8e1b9
Revises: e727c35d8423
Create Date: 2026-05-17
"""
from alembic import op
import sqlalchemy as sa

revision = 'f1a8e1b9'
down_revision = 'e727c35d8423'
branch_labels = None
depends_on = None

def upgrade() -> None:
    op.add_column(
        'import_batches',
        sa.Column('data_source', sa.String(50), nullable=False, server_default='local')
    )

def downgrade() -> None:
    op.drop_column('import_batches', 'data_source')
