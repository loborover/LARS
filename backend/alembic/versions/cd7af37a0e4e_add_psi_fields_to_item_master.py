"""add_psi_fields_to_item_master

Revision ID: cd7af37a0e4e
Revises: 5a44df45d409
Create Date: 2026-05-16 15:20:38.573026

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'cd7af37a0e4e'
down_revision: Union[str, Sequence[str], None] = '5a44df45d409'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column('item_master', sa.Column('lower_vendor_raw', sa.Text(), nullable=True))
    op.add_column('item_master', sa.Column('inventory_qty', sa.Numeric(precision=12, scale=4), server_default='0', nullable=False))
    op.add_column('item_master', sa.Column('defect_qty', sa.Numeric(precision=12, scale=4), server_default='0', nullable=False))
    op.add_column('item_master', sa.Column('is_picked', sa.Boolean(), server_default='false', nullable=False))


def downgrade() -> None:
    op.drop_column('item_master', 'is_picked')
    op.drop_column('item_master', 'defect_qty')
    op.drop_column('item_master', 'inventory_qty')
    op.drop_column('item_master', 'lower_vendor_raw')
